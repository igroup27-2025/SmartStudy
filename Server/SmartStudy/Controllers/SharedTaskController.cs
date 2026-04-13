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

        var responder = await _db.GetUserByEmailAsync(email);
        var responderName = responder != null ? $"{responder.FirstName} {responder.LastName}" : email;
        var task = await _db.GetTaskByIdAsync(taskId);
        var taskTitle = task?.Title ?? "Task";
        await _notifications.CreateSharedTaskResponseNotificationAsync(
            sharedInfo.CreatedByEmail, responderName, taskId, taskTitle, dto.Accept);

        if (sharedStatus == "Confirmed" && task != null)
        {
            // Create the accepting partner's copy (if missing), find a mutual-free
            // slot, and place NeedReview events on BOTH calendars at the same
            // time. If no mutual slot exists, the helper mirrors the creator's
            // current schedule onto the partner — either way, both sides end up
            // with events at identical times.
            var foundCommonTime = await _scheduling.EnsurePartnerCopyAndScheduleAsync(
                taskId, sharedInfo.CreatedByEmail, email);

            if (!foundCommonTime)
            {
                await _notifications.CreateNoCommonTimeNotificationAsync(
                    sharedInfo.CreatedByEmail, taskId, taskTitle);
                await _notifications.CreateNoCommonTimeNotificationAsync(
                    email, taskId, taskTitle);
            }

            // Reschedule every member's remaining tasks around the locked shared slot.
            var memberEmails = await _db.GetSharedTaskMemberEmailsAsync(taskId);
            foreach (var memberEmail in memberEmails)
                await _scheduling.ScheduleAllTasksAsync(memberEmail);
        }
        else if (sharedStatus == "Cancelled")
        {
            // Decline removes the partner's copy and any already-placed events so
            // the accepting user's calendar doesn't keep a stale shared slot.
            await _db.CleanupSharedTaskPartnerCopiesAsync(taskId);
            await _scheduling.ScheduleAllTasksAsync(sharedInfo.CreatedByEmail);
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
        if (sharedInfo == null || !string.Equals(sharedInfo.CreatedByEmail, email, StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = "Shared task not found" });

        await _db.UpdateSharedTaskStatusAsync(taskId, "Cancelled");

        var task = await _db.GetTaskByIdAsync(taskId);
        var taskTitle = task?.Title ?? "Task";
        var creator = await _db.GetUserByEmailAsync(email);
        var creatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : email;

        var affectedPartners = sharedInfo.Members
            .Where(m => !string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Email)
            .ToList();

        // Remove each partner's copy task + its events before they're stranded
        // with an orphaned slot on their calendar.
        await _db.CleanupSharedTaskPartnerCopiesAsync(taskId);

        foreach (var partnerEmail in affectedPartners)
        {
            await _notifications.CreateSharedTaskResponseNotificationAsync(
                partnerEmail, creatorName, taskId, taskTitle, false);
            await _scheduling.ScheduleAllTasksAsync(partnerEmail);
        }

        await _scheduling.ScheduleAllTasksAsync(email);

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
