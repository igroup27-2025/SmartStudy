using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/shared-tasks")]
[Authorize]
public class SharedTaskController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly NotificationService _notifications;
    private readonly SchedulingService _scheduling;

    public SharedTaskController(SmartStudyDbContext db, NotificationService notifications, SchedulingService scheduling)
    {
        _db = db;
        _notifications = notifications;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    private static (string, string) NormalizePair(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? (a, b) : (b, a);

    /// <summary>
    /// List all shared tasks the current user is a member of.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var email = GetEmail();

        var sharedTasks = await _db.SharedTaskMembers
            .Where(m => m.Email == email)
            .Include(m => m.SharedTask)
                .ThenInclude(st => st.Task)
                    .ThenInclude(t => t.Course)
            .Include(m => m.SharedTask)
                .ThenInclude(st => st.CreatedBy)
            .Include(m => m.SharedTask)
                .ThenInclude(st => st.Members)
                    .ThenInclude(mem => mem.User)
            .Select(m => m.SharedTask)
            .Distinct()
            .ToListAsync();

        var result = sharedTasks.Select(MapToDto).ToList();
        return Ok(result);
    }

    /// <summary>
    /// Get a single shared task by TaskId.
    /// </summary>
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetById(int taskId)
    {
        var email = GetEmail();

        var sharedTask = await _db.SharedTasks
            .Include(st => st.Task).ThenInclude(t => t.Course)
            .Include(st => st.CreatedBy)
            .Include(st => st.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(st => st.TaskId == taskId);

        if (sharedTask == null)
            return NotFound(new { message = "Shared task not found" });

        if (!sharedTask.Members.Any(m => m.Email == email))
            return NotFound(new { message = "Shared task not found" });

        return Ok(MapToDto(sharedTask));
    }

    /// <summary>
    /// Share an existing task with a friend. Creates SharedTask + 2 SharedTaskMembers.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSharedTaskDto dto)
    {
        var email = GetEmail();

        // Verify the task belongs to current user
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.TaskId == dto.TaskId && t.Email == email);
        if (task == null)
            return NotFound(new { message = "Task not found" });

        // Check not already shared
        var existing = await _db.SharedTasks.FirstOrDefaultAsync(st => st.TaskId == dto.TaskId);
        if (existing != null)
            return BadRequest(new { message = "Task is already shared" });

        // Verify friendship exists
        var (e1, e2) = NormalizePair(email, dto.PartnerEmail);
        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.Email1 == e1 && f.Email2 == e2 && f.IsActive);
        if (friendship == null)
            return BadRequest(new { message = "You must be friends to share a task" });

        var sharedTask = new SharedTask
        {
            TaskId = dto.TaskId,
            CreatedByEmail = email,
            CreatedAt = DateTime.Now,
            SharedStatus = "Pending"
        };

        _db.SharedTasks.Add(sharedTask);

        // Creator auto-accepts
        _db.SharedTaskMembers.AddRange(
            new SharedTaskMember
            {
                TaskId = dto.TaskId,
                Email = email,
                ResponseStatus = "Accepted",
                RespondedAt = DateTime.Now
            },
            new SharedTaskMember
            {
                TaskId = dto.TaskId,
                Email = dto.PartnerEmail,
                ResponseStatus = "Pending"
            }
        );

        await _db.SaveChangesAsync();

        // Notify the partner
        var sender = await _db.Users.FindAsync(email);
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

        var member = await _db.SharedTaskMembers
            .Include(m => m.SharedTask)
            .FirstOrDefaultAsync(m => m.TaskId == taskId && m.Email == email && m.ResponseStatus == "Pending");

        if (member == null)
            return NotFound(new { message = "No pending invitation found" });

        member.ResponseStatus = dto.Accept ? "Accepted" : "Declined";
        member.RespondedAt = DateTime.Now;

        // Update shared task status
        if (dto.Accept)
        {
            // Check if all members accepted
            var allMembers = await _db.SharedTaskMembers
                .Where(m => m.TaskId == taskId)
                .ToListAsync();

            var allAccepted = allMembers.All(m => m.ResponseStatus == "Accepted" || m.Email == email);
            if (allAccepted)
                member.SharedTask.SharedStatus = "Confirmed";
        }
        else
        {
            member.SharedTask.SharedStatus = "Cancelled";
        }

        await _db.SaveChangesAsync();

        // Notify the creator about the response
        var responder = await _db.Users.FindAsync(email);
        var responderName = responder != null ? $"{responder.FirstName} {responder.LastName}" : email;
        var task = await _db.Tasks.Include(t => t.Course).FirstOrDefaultAsync(t => t.TaskId == taskId);
        var taskTitle = task?.Title ?? "Task";
        await _notifications.CreateSharedTaskResponseNotificationAsync(
            member.SharedTask.CreatedByEmail, responderName, taskId, taskTitle, dto.Accept);

        // When confirmed, create a task copy for the accepting user and reschedule both
        if (member.SharedTask.SharedStatus == "Confirmed" && task != null)
        {
            // Check if accepting user already has a copy
            var existingCopy = await _db.Tasks
                .FirstOrDefaultAsync(t => t.Email == email && t.Title == task.Title
                    && t.CourseId == task.CourseId && t.DueDate == task.DueDate);

            if (existingCopy == null)
            {
                // Ensure the accepting user is enrolled in the same course
                var enrolled = await _db.UserCourses.AnyAsync(uc => uc.Email == email && uc.CourseId == task.CourseId);
                if (!enrolled)
                {
                    _db.UserCourses.Add(new UserCourse { Email = email, CourseId = task.CourseId });
                    await _db.SaveChangesAsync();
                }

                var taskCopy = new StudentTask
                {
                    CourseId = task.CourseId,
                    Title = task.Title,
                    Type = task.Type,
                    EstimatedHours = task.EstimatedHours,
                    DueDate = task.DueDate,
                    Priority = task.Priority,
                    Email = email,
                    IsCompleted = false,
                    AllowSplitting = task.AllowSplitting
                };
                _db.Tasks.Add(taskCopy);
                await _db.SaveChangesAsync();
            }

            var allMembers = await _db.SharedTaskMembers
                .Where(m => m.TaskId == taskId)
                .Select(m => m.Email)
                .ToListAsync();
            foreach (var memberEmail in allMembers)
            {
                await _scheduling.ScheduleAllTasksAsync(memberEmail);
            }
        }

        return Ok(new { taskId, status = member.SharedTask.SharedStatus });
    }

    /// <summary>
    /// Cancel a shared task (only the creator can cancel).
    /// </summary>
    [HttpPost("{taskId}/cancel")]
    public async Task<IActionResult> Cancel(int taskId)
    {
        var email = GetEmail();

        var sharedTask = await _db.SharedTasks
            .FirstOrDefaultAsync(st => st.TaskId == taskId && st.CreatedByEmail == email);

        if (sharedTask == null)
            return NotFound(new { message = "Shared task not found" });

        sharedTask.SharedStatus = "Cancelled";
        await _db.SaveChangesAsync();

        return Ok(new { taskId, status = "Cancelled" });
    }

    private static SharedTaskDto MapToDto(SharedTask st) => new()
    {
        TaskId = st.TaskId,
        TaskTitle = st.Task.Title,
        CourseId = st.Task.CourseId,
        CourseName = st.Task.Course?.CourseName,
        CreatedByEmail = st.CreatedByEmail,
        CreatedByName = $"{st.CreatedBy.FirstName} {st.CreatedBy.LastName}",
        CreatedAt = st.CreatedAt,
        SharedStatus = st.SharedStatus,
        Members = st.Members.Select(m => new SharedTaskMemberDto
        {
            Email = m.Email,
            Name = $"{m.User.FirstName} {m.User.LastName}",
            ResponseStatus = m.ResponseStatus,
            RespondedAt = m.RespondedAt
        }).ToList()
    };
}
