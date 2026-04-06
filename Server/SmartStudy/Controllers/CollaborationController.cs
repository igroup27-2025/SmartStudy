using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/collaboration")]
[Authorize]
public class CollaborationController : ControllerBase
{
    private readonly DBservices _db;
    private readonly SafeZoneService _safeZoneService;
    private readonly SchedulingService _scheduling;

    public CollaborationController(DBservices db, SafeZoneService safeZoneService, SchedulingService scheduling)
    {
        _db = db;
        _safeZoneService = safeZoneService;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// Get safe study zones (mutual free time with low stress) for a connection.
    /// </summary>
    [HttpGet("safe-zones")]
    public async Task<IActionResult> GetSafeZones([FromQuery] int connectionId)
    {
        var email = GetEmail();

        var friendship = await _db.GetFriendshipForUserAsync(connectionId, email);
        if (friendship == null)
            return NotFound(new { message = "Friendship not found or not active" });

        var friendEmail = friendship.Email1 == email
            ? friendship.Email2
            : friendship.Email1;

        var zones = await _safeZoneService.GetSafeZonesAsync(email, friendEmail);
        return Ok(zones);
    }

    /// <summary>
    /// Approve all future course sharing for a specific course.
    /// Auto-accepts all pending shared tasks from that course and enables auto-sharing.
    /// </summary>
    [HttpPost("approve-course-sharing/{courseId}")]
    public async Task<IActionResult> ApproveCourseSharing(int courseId)
    {
        var email = GetEmail();

        var userCourse = await _db.GetUserCourseAsync(email, courseId);
        if (userCourse == null)
            return NotFound(new { message = "Course enrollment not found" });

        await _db.SetCourseShareApprovedAsync(email, courseId);

        // Get pending shared task members for this course
        var pendingMembers = await _db.GetPendingMembersForCourseAsync(email, courseId);

        int accepted = 0;
        var confirmedTasks = new List<(int TaskId, string CreatorEmail)>();

        foreach (var member in pendingMembers)
        {
            // Accept the member
            await _db.UpdateSharedTaskMemberStatusAsync(member.TaskId, email, "Accepted");

            // Check if all members now accepted
            var allAccepted = await _db.AllSharedTaskMembersAcceptedAsync(member.TaskId);
            if (allAccepted)
            {
                await _db.UpdateSharedTaskStatusAsync(member.TaskId, "Confirmed");
                confirmedTasks.Add((member.TaskId, member.CreatedByEmail));
            }
            accepted++;
        }

        // Create task copies and schedule with common time for each confirmed shared task
        foreach (var (taskId, creatorEmail) in confirmedTasks)
        {
            var task = await _db.GetTaskByIdAsync(taskId);
            if (task == null) continue;

            // Check if accepting user already has a copy
            var userTasks = await _db.GetTasksByUserAsync(email, task.CourseId);
            var existingCopy = userTasks.FirstOrDefault(t =>
                t.Title == task.Title && t.CourseId == task.CourseId && t.DueDate == task.DueDate);

            if (existingCopy == null)
            {
                await _db.CreateTaskAsync(
                    task.CourseId, email, task.Title, task.Type,
                    task.EstimatedHours, task.DueDate, null,
                    task.AllowSplitting, task.Priority, task.IsManualPriority);
            }

            // Schedule all tasks first, then override shared task with common time
            await _scheduling.ScheduleAllTasksAsync(email);
            await _scheduling.ScheduleAllTasksAsync(creatorEmail);
            await _scheduling.ScheduleSharedTaskAtCommonTimeAsync(taskId, creatorEmail, email);
        }

        return Ok(new { message = $"Course sharing approved. {accepted} pending tasks auto-accepted.", accepted });
    }
}
