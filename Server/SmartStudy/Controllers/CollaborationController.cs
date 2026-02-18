using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/collaboration")]
[Authorize]
public class CollaborationController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly SafeZoneService _safeZoneService;

    public CollaborationController(SmartStudyDbContext db, SafeZoneService safeZoneService)
    {
        _db = db;
        _safeZoneService = safeZoneService;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// Get safe study zones (mutual free time with low stress) for a connection.
    /// </summary>
    [HttpGet("safe-zones")]
    public async Task<IActionResult> GetSafeZones([FromQuery] int connectionId)
    {
        var email = GetEmail();

        var friendship = await _db.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == connectionId &&
                (f.Email1 == email || f.Email2 == email) &&
                f.IsActive);

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

        var userCourse = await _db.UserCourses.FirstOrDefaultAsync(
            uc => uc.Email == email && uc.CourseId == courseId);
        if (userCourse == null)
            return NotFound(new { message = "Course enrollment not found" });

        userCourse.CourseShareApproved = true;
        await _db.SaveChangesAsync();

        // Auto-accept all pending shared tasks for this course
        var pendingMembers = await _db.SharedTaskMembers
            .Include(m => m.SharedTask)
            .ThenInclude(st => st.Task)
            .Where(m => m.Email == email && m.ResponseStatus == "Pending"
                && m.SharedTask.Task.CourseId == courseId)
            .ToListAsync();

        int accepted = 0;
        foreach (var member in pendingMembers)
        {
            member.ResponseStatus = "Accepted";
            member.RespondedAt = DateTime.UtcNow;

            // Update shared task status if all members accepted
            var allMembers = await _db.SharedTaskMembers
                .Where(m => m.TaskId == member.TaskId)
                .ToListAsync();
            if (allMembers.All(m => m.ResponseStatus == "Accepted"))
            {
                member.SharedTask.SharedStatus = "Confirmed";
            }
            accepted++;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Course sharing approved. {accepted} pending tasks auto-accepted.", accepted });
    }
}
