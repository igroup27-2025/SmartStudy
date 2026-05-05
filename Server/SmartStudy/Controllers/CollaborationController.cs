using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

// API endpoints for peer collaboration: shared safe-zones and course-sharing approval.
[ApiController]
[Route("api/collaboration")]
[Authorize]
public class CollaborationController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Returns intersected low-stress availability slots between the two friends.
    [HttpGet("safe-zones")]
    public IActionResult GetSafeZones([FromQuery] int connectionId)
    {
        var email = GetEmail();

        var friendship = Friendship.GetForUser(connectionId, email);
        if (friendship == null)
            return NotFound(new { message = "Friendship not found or not active" });

        var friendEmail = friendship.Email1 == email
            ? friendship.Email2
            : friendship.Email1;

        var zones = Friendship.GetSafeZones(email, friendEmail);
        return Ok(zones);
    }

    // Marks a course as shareable, auto-accepts pending shared tasks, and re-schedules.
    [HttpPost("approve-course-sharing/{courseId}")]
    public IActionResult ApproveCourseSharing(int courseId)
    {
        var email = GetEmail();

        var userCourse = UserCourse.Get(email, courseId);
        if (userCourse == null)
            return NotFound(new { message = "Course enrollment not found" });

        UserCourse.SetCourseShareApproved(email, courseId);

        var pendingMembers = UserCourse.GetPendingMembersForCourse(email, courseId);

        int accepted = 0;
        var confirmedTasks = new List<(int TaskId, string CreatorEmail)>();

        foreach (var member in pendingMembers)
        {
            SharedTask.UpdateMemberStatus(member.TaskId, email, "Accepted");

            var allAccepted = SharedTask.AllMembersAccepted(member.TaskId);
            if (allAccepted)
            {
                SharedTask.UpdateStatus(member.TaskId, "Confirmed");
                confirmedTasks.Add((member.TaskId, member.CreatedByEmail));
            }
            accepted++;
        }

        foreach (var (taskId, creatorEmail) in confirmedTasks)
        {
            var task = StudentTask.GetById(taskId);
            if (task == null) continue;

            var userTasks = StudentTask.GetByUser(email, task.CourseId);
            var existingCopy = userTasks.FirstOrDefault(t =>
                t.Title == task.Title && t.CourseId == task.CourseId && t.DueDate == task.DueDate);

            if (existingCopy == null)
            {
                StudentTask.Create(
                    task.CourseId, email, task.Title, task.Type,
                    task.EstimatedHours, task.DueDate, null,
                    task.AllowSplitting, task.Priority, task.IsManualPriority);
            }

            StudentTask.ScheduleAll(email);
            StudentTask.ScheduleAll(creatorEmail);
            StudentTask.ScheduleSharedTaskAtCommonTime(taskId, creatorEmail, email);
        }

        return Ok(new { message = $"Course sharing approved. {accepted} pending tasks auto-accepted.", accepted });
    }
}
