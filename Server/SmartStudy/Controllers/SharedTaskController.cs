using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Models;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

// API endpoints for sharing tasks with peers: invite, respond, cancel.
[ApiController]
[Route("api/shared-tasks")]
[Authorize]
public class SharedTaskController : ControllerBase
{
    private readonly ILogger<SharedTaskController> _logger;

    // Injects the logger.
    public SharedTaskController(ILogger<SharedTaskController> logger)
    {
        _logger = logger;
    }

    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Returns all shared tasks the user is a member of.
    [HttpGet]
    public IActionResult GetAll()
    {
        var email = GetEmail();
        var rows = SharedTask.GetByUser(email);
        var dtos = GroupRowsIntoDtos(rows);
        return Ok(dtos);
    }

    // Returns one shared task with all its members if the caller is a member.
    [HttpGet("{taskId}")]
    public IActionResult GetById(int taskId)
    {
        var email = GetEmail();
        var rows = SharedTask.GetByTaskId(taskId);

        if (!rows.Any())
            return NotFound(new { message = "Shared task not found" });

        if (!rows.Any(r => r.MemberEmail == email))
            return NotFound(new { message = "Shared task not found" });

        var dtos = GroupRowsIntoDtos(rows);
        return Ok(dtos.First());
    }

    // Invites a friend to share an owned task and notifies them.
    [HttpPost]
    public IActionResult Create([FromBody] CreateSharedTaskDto dto)
    {
        var email = GetEmail();

        var task = StudentTask.GetById(dto.TaskId);
        if (task == null || task.Email != email)
            return NotFound(new { message = "Task not found" });

        var alreadyShared = SharedTask.Exists(dto.TaskId);
        if (alreadyShared)
            return BadRequest(new { message = "Task is already shared" });

        var friendshipExists = Friendship.ExistsBetween(email, dto.PartnerEmail);
        if (!friendshipExists)
            return BadRequest(new { message = "You must be friends to share a task" });

        SharedTask.Create(dto.TaskId, email, "Pending");
        SharedTask.CreateMember(dto.TaskId, email, "Accepted", DateTime.UtcNow);
        SharedTask.CreateMember(dto.TaskId, dto.PartnerEmail, "Pending");

        var sender = UserModel.GetByEmail(email);
        var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : email;
        Notification.CreateSharedTaskInvite(dto.PartnerEmail, senderName, dto.TaskId, task.Title);

        return CreatedAtAction(nameof(GetById), new { taskId = dto.TaskId }, new { taskId = dto.TaskId, status = "Pending" });
    }

    // Accepts or declines a shared-task invite, then re-schedules both partners on accept.
    [HttpPost("{taskId}/respond")]
    public IActionResult Respond(int taskId, [FromBody] RespondSharedTaskDto dto)
    {
        var email = GetEmail();

        var updated = SharedTask.UpdateMemberStatus(taskId, email, dto.Accept ? "Accepted" : "Declined");
        if (!updated)
            return NotFound(new { message = "No pending invitation found" });

        var sharedInfo = StudentTask.GetSharedInfo(taskId);
        if (sharedInfo == null)
            return NotFound(new { message = "Shared task not found" });

        string sharedStatus;
        if (dto.Accept)
        {
            var allAccepted = SharedTask.AllMembersAccepted(taskId);
            sharedStatus = allAccepted ? "Confirmed" : sharedInfo.SharedStatus;
            if (allAccepted)
                SharedTask.UpdateStatus(taskId, "Confirmed");
        }
        else
        {
            sharedStatus = "Cancelled";
            SharedTask.UpdateStatus(taskId, "Cancelled");
        }

        var responder = UserModel.GetByEmail(email);
        var responderName = responder != null ? $"{responder.FirstName} {responder.LastName}" : email;
        var task = StudentTask.GetById(taskId);
        var taskTitle = task?.Title ?? "Task";
        Notification.CreateSharedTaskResponse(
            sharedInfo.CreatedByEmail, responderName, taskId, taskTitle, dto.Accept);

        try
        {
            if (sharedStatus == "Confirmed" && task != null)
            {
                var foundCommonTime = StudentTask.EnsurePartnerCopyAndSchedule(
                    taskId, sharedInfo.CreatedByEmail, email);

                if (!foundCommonTime)
                {
                    Notification.CreateNoCommonTime(sharedInfo.CreatedByEmail, taskId, taskTitle);
                    Notification.CreateNoCommonTime(email, taskId, taskTitle);
                }

                var memberEmails = SharedTask.GetMemberEmails(taskId);
                foreach (var memberEmail in memberEmails)
                    StudentTask.ScheduleAll(memberEmail);
            }
            else if (sharedStatus == "Cancelled")
            {
                SharedTask.CleanupPartnerCopies(taskId);
                StudentTask.ScheduleAll(sharedInfo.CreatedByEmail);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shared task {TaskId} response saved but follow-up scheduling failed", taskId);
        }

        return Ok(new { taskId, status = sharedStatus });
    }

    // Cancels an owned shared task, cleans up partner copies, and notifies them.
    [HttpPost("{taskId}/cancel")]
    public IActionResult Cancel(int taskId)
    {
        var email = GetEmail();

        var sharedInfo = StudentTask.GetSharedInfo(taskId);
        if (sharedInfo == null || !string.Equals(sharedInfo.CreatedByEmail, email, StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = "Shared task not found" });

        SharedTask.UpdateStatus(taskId, "Cancelled");

        var task = StudentTask.GetById(taskId);
        var taskTitle = task?.Title ?? "Task";
        var creator = UserModel.GetByEmail(email);
        var creatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : email;

        var affectedPartners = sharedInfo.Members
            .Where(m => !string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Email)
            .ToList();

        SharedTask.CleanupPartnerCopies(taskId);

        foreach (var partnerEmail in affectedPartners)
        {
            Notification.CreateSharedTaskResponse(
                partnerEmail, creatorName, taskId, taskTitle, false);
            StudentTask.ScheduleAll(partnerEmail);
        }

        StudentTask.ScheduleAll(email);

        return Ok(new { taskId, status = "Cancelled" });
    }

    // Folds the flat join rows into one SharedTaskDto per task with member sub-list.
    private static List<SharedTaskDto> GroupRowsIntoDtos(List<SharedTaskFullRow> rows)
    {
        return rows
            .GroupBy(r => r.TaskId)
            .Select(g =>
            {
                var first = g.First();
                return new SharedTaskDto
                {
                    TaskId = first.TaskId,
                    TaskTitle = first.TaskTitle,
                    CourseId = first.CourseId,
                    CourseName = first.CourseName,
                    CreatedByEmail = first.CreatedByEmail,
                    CreatedByName = $"{first.CreatorFirstName} {first.CreatorLastName}",
                    CreatedAt = first.CreatedAt,
                    SharedStatus = first.SharedStatus,
                    Members = g.Select(r => new SharedTaskMemberDto
                    {
                        Email = r.MemberEmail,
                        Name = $"{r.MemberFirstName} {r.MemberLastName}",
                        ResponseStatus = r.ResponseStatus,
                        RespondedAt = r.RespondedAt
                    }).DistinctBy(m => m.Email).ToList()
                };
            })
            .ToList();
    }
}
