using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/shared-tasks")]
[Authorize]
public class SharedTaskController : ControllerBase
{
    private readonly DBservices _db;
    private readonly NotificationService _notifications;
    private readonly SchedulingService _scheduling;

    public SharedTaskController(DBservices db, NotificationService notifications, SchedulingService scheduling)
    {
        _db = db;
        _notifications = notifications;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// List all shared tasks the current user is a member of.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();
        var rows = await _db.GetSharedTasksByUserAsync(email);
        var dtos = GroupRowsIntoDtos(rows);
        return Ok(dtos);
    }

    /// <summary>
    /// Get a single shared task by TaskId.
    /// </summary>
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetById(int taskId)
    {
        var email = GetEmail();
        var rows = await _db.GetSharedTaskByTaskIdAsync(taskId);

        if (!rows.Any())
            return NotFound(new { message = "Shared task not found" });

        // Check membership
        if (!rows.Any(r => r.MemberEmail == email))
            return NotFound(new { message = "Shared task not found" });

        var dtos = GroupRowsIntoDtos(rows);
        return Ok(dtos.First());
    }

    /// <summary>
    /// Share an existing task with a friend. Creates SharedTask + 2 SharedTaskMembers.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSharedTaskDto dto)
    {
        var email = GetEmail();

        // Verify the task belongs to current user
        var task = await _db.GetTaskByIdAsync(dto.TaskId);
        if (task == null || task.Email != email)
            return NotFound(new { message = "Task not found" });

        // Check not already shared
        var alreadyShared = await _db.SharedTaskExistsAsync(dto.TaskId);
        if (alreadyShared)
            return BadRequest(new { message = "Task is already shared" });

        // Verify friendship exists
        var friendshipExists = await _db.FriendshipExistsAsync(email, dto.PartnerEmail);
        if (!friendshipExists)
            return BadRequest(new { message = "You must be friends to share a task" });

        // Create shared task
        await _db.CreateSharedTaskAsync(dto.TaskId, email, "Pending");

        // Creator auto-accepts
        await _db.CreateSharedTaskMemberAsync(dto.TaskId, email, "Accepted", DateTime.UtcNow);

        // Partner gets pending invitation
        await _db.CreateSharedTaskMemberAsync(dto.TaskId, dto.PartnerEmail, "Pending");

        // Notify the partner
        var sender = await _db.GetUserByEmailAsync(email);
        var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : email;
        await _notifications.CreateSharedTaskInviteNotificationAsync(dto.PartnerEmail, senderName, dto.TaskId, task.Title);

        return CreatedAtAction(nameof(GetById), new { taskId = dto.TaskId }, new { taskId = dto.TaskId, status = "Pending" });
    }

    /// <summary>
    /// Respond to a shared task invitation (accept or decline).
    /// </summary>
    [HttpPost("{taskId}/respond")]
    public async Task<IActionResult> Respond(int taskId, [FromBody] RespondSharedTaskDto dto)
    {
        var email = GetEmail();

        // Update member status
        var updated = await _db.UpdateSharedTaskMemberStatusAsync(taskId, email, dto.Accept ? "Accepted" : "Declined");
        if (!updated)
            return NotFound(new { message = "No pending invitation found" });

        // Get shared task info to find creator
        var sharedInfo = await _db.GetSharedInfoAsync(taskId);
        if (sharedInfo == null)
            return NotFound(new { message = "Shared task not found" });

        string sharedStatus;
        if (dto.Accept)
        {
            var allAccepted = await _db.AllSharedTaskMembersAcceptedAsync(taskId);
            sharedStatus = allAccepted ? "Confirmed" : sharedInfo.SharedStatus;
            if (allAccepted)
                await _db.UpdateSharedTaskStatusAsync(taskId, "Confirmed");
        }
        else
        {
            sharedStatus = "Cancelled";
            await _db.UpdateSharedTaskStatusAsync(taskId, "Cancelled");
        }

        // Notify the creator about the response
        var responder = await _db.GetUserByEmailAsync(email);
        var responderName = responder != null ? $"{responder.FirstName} {responder.LastName}" : email;
        var task = await _db.GetTaskByIdAsync(taskId);
        var taskTitle = task?.Title ?? "Task";
        await _notifications.CreateSharedTaskResponseNotificationAsync(
            sharedInfo.CreatedByEmail, responderName, taskId, taskTitle, dto.Accept);

        // When confirmed, create a task copy for the accepting user and reschedule both
        if (sharedStatus == "Confirmed" && task != null)
        {
            // Check if accepting user already has a copy (by title + course + due date)
            var userTasks = await _db.GetTasksByUserAsync(email, task.CourseId);
            var existingCopy = userTasks.FirstOrDefault(t =>
                t.Title == task.Title && t.CourseId == task.CourseId && t.DueDate == task.DueDate);

            if (existingCopy == null)
            {
                // Ensure the accepting user is enrolled in the same course
                var enrolled = await _db.UserCourseExistsAsync(email, task.CourseId);
                if (!enrolled)
                {
                    await _db.CreateUserCourseAsync(email, task.CourseId);
                }

                var copyTaskId = await _db.CreateTaskAsync(
                    task.CourseId, email, task.Title, task.Type,
                    task.EstimatedHours, task.DueDate, null,
                    task.AllowSplitting, task.Priority, task.IsManualPriority);

                // Link the copy back to the shared task so partner's task shows as shared
                await _db.UpdateSharedTaskMemberCopyTaskIdAsync(taskId, email, copyTaskId);
            }

            var memberEmails = await _db.GetSharedTaskMemberEmailsAsync(taskId);
            foreach (var memberEmail in memberEmails)
            {
                await _scheduling.ScheduleAllTasksAsync(memberEmail);
            }

            // Override the shared task's schedule with a common time slot for both users
            var foundCommonTime = await _scheduling.ScheduleSharedTaskAtCommonTimeAsync(
                taskId, sharedInfo.CreatedByEmail, email);

            if (!foundCommonTime)
            {
                await _notifications.CreateNoCommonTimeNotificationAsync(
                    sharedInfo.CreatedByEmail, taskId, taskTitle);
                await _notifications.CreateNoCommonTimeNotificationAsync(
                    email, taskId, taskTitle);
            }
        }

        return Ok(new { taskId, status = sharedStatus });
    }

    /// <summary>
    /// Cancel a shared task (only the creator can cancel).
    /// </summary>
    [HttpPost("{taskId}/cancel")]
    public async Task<IActionResult> Cancel(int taskId)
    {
        var email = GetEmail();

        var sharedInfo = await _db.GetSharedInfoAsync(taskId);
        if (sharedInfo == null || sharedInfo.CreatedByEmail != email)
            return NotFound(new { message = "Shared task not found" });

        await _db.UpdateSharedTaskStatusAsync(taskId, "Cancelled");

        // Notify other members about the cancellation
        var task = await _db.GetTaskByIdAsync(taskId);
        var taskTitle = task?.Title ?? "Task";
        var creator = await _db.GetUserByEmailAsync(email);
        var creatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : email;
        foreach (var member in sharedInfo.Members.Where(m => m.Email != email))
        {
            await _notifications.CreateSharedTaskResponseNotificationAsync(
                member.Email, creatorName, taskId, taskTitle, false);
        }

        return Ok(new { taskId, status = "Cancelled" });
    }

    /// <summary>
    /// Group flat SP rows into SharedTaskDto list.
    /// </summary>
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
