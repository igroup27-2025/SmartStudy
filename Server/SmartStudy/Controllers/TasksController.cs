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
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly SchedulingService _scheduling;

    public TasksController(SmartStudyDbContext db, SchedulingService scheduling)
    {
        _db = db;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId, [FromQuery] bool? completed)
    {
        var email = GetEmail();
        var query = _db.Tasks
            .Include(t => t.Course)
            .Include(t => t.TaskEvents)
            .Include(t => t.SubTasks)
            .Include(t => t.SharedTask).ThenInclude(st => st!.Members).ThenInclude(m => m.User)
            .Where(t => t.Email == email && t.ParentTaskId == null);

        if (courseId.HasValue)
            query = query.Where(t => t.CourseId == courseId.Value);
        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        var tasks = await query
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ToListAsync();

        // Load sub-tasks with their events
        var taskIds = tasks.Select(t => t.TaskId).ToList();
        var subTasks = await _db.Tasks
            .Include(t => t.Course)
            .Include(t => t.TaskEvents)
            .Where(t => t.ParentTaskId != null && taskIds.Contains(t.ParentTaskId.Value))
            .ToListAsync();

        // Attach sub-tasks to parents
        foreach (var parent in tasks)
        {
            parent.SubTasks = subTasks.Where(s => s.ParentTaskId == parent.TaskId).ToList();
        }

        var dtos = tasks.Select(t => BuildTaskDto(t)).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks
            .Include(t => t.Course)
            .Include(t => t.TaskEvents)
            .Include(t => t.SubTasks)
            .Include(t => t.SharedTask).ThenInclude(st => st!.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);

        if (task == null) return NotFound();

        return Ok(BuildTaskDto(task));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskDto dto)
    {
        var email = GetEmail();

        var task = new StudentTask
        {
            CourseId = dto.CourseId,
            Title = dto.Title,
            Type = dto.Type,
            EstimatedHours = dto.EstimatedHours,
            DueDate = dto.DueDate,
            ParentTaskId = dto.ParentTaskId,
            Email = email,
            IsCompleted = false,
            AllowSplitting = dto.AllowSplitting
        };

        // If sub-task, inherit course/due date from parent
        if (dto.ParentTaskId.HasValue)
        {
            var parent = await _db.Tasks.FindAsync(dto.ParentTaskId.Value);
            if (parent == null || parent.Email != email)
                return BadRequest(new { message = "Parent task not found" });
            if (task.CourseId == 0) task.CourseId = parent.CourseId;
            if (!task.DueDate.HasValue) task.DueDate = parent.DueDate;
        }

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        // Auto-share if course has SharedByDefault enabled and has a study partner
        var userCourse = await _db.UserCourses.FirstOrDefaultAsync(uc => uc.Email == email && uc.CourseId == task.CourseId);
        if (userCourse?.SharedByDefault == true && !string.IsNullOrEmpty(userCourse.StudyPartnerEmail))
        {
            var sharedTask = new SharedTask
            {
                TaskId = task.TaskId,
                CreatedByEmail = email,
                SharedStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _db.SharedTasks.Add(sharedTask);
            await _db.SaveChangesAsync();

            // Check if partner has approved course sharing
            var partnerUserCourse = await _db.UserCourses.FirstOrDefaultAsync(
                uc => uc.Email == userCourse.StudyPartnerEmail && uc.CourseId == task.CourseId);
            var autoApproved = partnerUserCourse?.CourseShareApproved == true;

            _db.SharedTaskMembers.Add(new SharedTaskMember
            {
                TaskId = task.TaskId,
                Email = userCourse.StudyPartnerEmail,
                ResponseStatus = autoApproved ? "Accepted" : "Pending",
                RespondedAt = autoApproved ? DateTime.UtcNow : null
            });

            if (autoApproved) sharedTask.SharedStatus = "Confirmed";

            await _db.SaveChangesAsync();
        }

        // Trigger scheduling for all tasks
        await _scheduling.ScheduleAllTasksAsync(email);

        // Reload with scheduling info
        var reloaded = await _db.Tasks
            .Include(t => t.Course).Include(t => t.TaskEvents).Include(t => t.SubTasks)
            .FirstAsync(t => t.TaskId == task.TaskId);

        return CreatedAtAction(nameof(Get), new { id = task.TaskId }, BuildTaskDto(reloaded));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents).Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);

        if (task == null) return NotFound();

        if (dto.CourseId.HasValue) task.CourseId = dto.CourseId.Value;
        if (dto.Title != null) task.Title = dto.Title;
        if (dto.Type != null) task.Type = dto.Type;
        if (dto.EstimatedHours.HasValue) task.EstimatedHours = dto.EstimatedHours;
        if (dto.DueDate.HasValue) task.DueDate = dto.DueDate;
        if (dto.IsCompleted.HasValue) task.IsCompleted = dto.IsCompleted.Value;
        if (dto.AllowSplitting.HasValue) task.AllowSplitting = dto.AllowSplitting.Value;
        if (dto.IsManuallyPinned.HasValue) task.IsManuallyPinned = dto.IsManuallyPinned.Value;

        await _db.SaveChangesAsync();

        // Trigger rescheduling
        await _scheduling.ScheduleAllTasksAsync(email);

        // Reload with scheduling info
        var reloaded = await _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents).Include(t => t.SubTasks)
            .FirstAsync(t => t.TaskId == task.TaskId);

        return Ok(BuildTaskDto(reloaded));
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteTaskDto? dto = null)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.TaskEvents).Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);
        if (task == null) return NotFound();

        task.IsCompleted = !task.IsCompleted;

        // Store actual hours if completing (not uncompleting)
        if (task.IsCompleted && dto?.ActualHours.HasValue == true)
        {
            task.ActualHours = dto.ActualHours;
        }
        if (!task.IsCompleted)
        {
            task.ActualHours = null;
        }

        // Remove task events when completing
        if (task.IsCompleted && task.TaskEvents.Any())
        {
            var eventIds = task.TaskEvents.Select(te => te.EventId).ToList();
            var events = await _db.Events.Where(e => eventIds.Contains(e.EventId)).ToListAsync();
            _db.Events.RemoveRange(events);
        }

        await _db.SaveChangesAsync();

        // If this is a sub-task and all siblings are complete, optionally complete parent
        if (task.ParentTaskId.HasValue && task.IsCompleted)
        {
            var siblings = await _db.Tasks.Where(t => t.ParentTaskId == task.ParentTaskId).ToListAsync();
            if (siblings.All(s => s.IsCompleted))
            {
                var parent = await _db.Tasks.Include(t => t.TaskEvents).FirstOrDefaultAsync(t => t.TaskId == task.ParentTaskId);
                if (parent != null && !parent.IsCompleted)
                {
                    parent.IsCompleted = true;
                    if (parent.TaskEvents.Any())
                    {
                        var parentEventIds = parent.TaskEvents.Select(te => te.EventId).ToList();
                        var parentEvents = await _db.Events.Where(e => parentEventIds.Contains(e.EventId)).ToListAsync();
                        _db.Events.RemoveRange(parentEvents);
                    }
                    await _db.SaveChangesAsync();
                }
            }
        }

        // Reschedule remaining tasks
        await _scheduling.ScheduleAllTasksAsync(email);

        // Compute ML stats for the task's course
        object mlStats = null;
        if (task.IsCompleted && task.CourseId > 0)
        {
            var courseTasks = await _db.Tasks
                .Where(t => t.Email == email && t.CourseId == task.CourseId
                    && t.IsCompleted && t.ActualHours.HasValue && t.EstimatedHours.HasValue && t.EstimatedHours > 0)
                .Select(t => new { t.ActualHours, t.EstimatedHours })
                .ToListAsync();

            if (courseTasks.Any())
            {
                var avgRatio = courseTasks.Average(t => (double)t.ActualHours.Value / (double)t.EstimatedHours.Value);
                var bias = avgRatio > 1.2 ? "underestimate" : avgRatio < 0.8 ? "overestimate" : "accurate";
                mlStats = new { courseAvgRatio = avgRatio, estimationBias = bias, sampleSize = courseTasks.Count };
            }
        }

        return Ok(new { task.TaskId, task.IsCompleted, task.ActualHours, mlStats });
    }

    [HttpPost("{id}/split")]
    public async Task<IActionResult> Split(int id, [FromBody] SplitTaskDto dto)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);
        if (task == null) return NotFound();
        if (task.ParentTaskId.HasValue) return BadRequest(new { message = "Cannot split a sub-task" });

        var created = new List<StudentTask>();
        foreach (var sub in dto.SubTasks)
        {
            var subTask = new StudentTask
            {
                CourseId = task.CourseId,
                Title = sub.Title,
                Type = task.Type,
                EstimatedHours = sub.EstimatedHours,
                DueDate = sub.DueDate ?? task.DueDate,
                ParentTaskId = task.TaskId,
                Email = email,
                IsCompleted = false
            };

            _db.Tasks.Add(subTask);
            created.Add(subTask);
        }

        await _db.SaveChangesAsync();
        await _scheduling.ScheduleAllTasksAsync(email);

        // Reload parent with sub-tasks
        var reloaded = await _db.Tasks
            .Include(t => t.Course).Include(t => t.TaskEvents).Include(t => t.SubTasks)
            .FirstAsync(t => t.TaskId == task.TaskId);

        return Ok(BuildTaskDto(reloaded));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);
        if (task == null) return NotFound();

        // Cascade-delete sub-tasks manually
        if (task.SubTasks.Any())
        {
            _db.Tasks.RemoveRange(task.SubTasks);
        }

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();

        // Reschedule remaining tasks (cascade already deleted TaskEvents)
        await _scheduling.ScheduleAllTasksAsync(email);

        return NoContent();
    }

    // ML Learning: suggest hours based on past actual/estimated ratio
    [HttpGet("suggest-hours")]
    public async Task<IActionResult> SuggestHours([FromQuery] int courseId, [FromQuery] decimal? estimatedHours)
    {
        var email = GetEmail();
        var completedTasks = await _db.Tasks
            .Where(t => t.Email == email && t.IsCompleted && t.CourseId == courseId
                && t.ActualHours.HasValue && t.EstimatedHours.HasValue && t.EstimatedHours > 0)
            .Select(t => new { t.ActualHours, t.EstimatedHours })
            .ToListAsync();

        if (completedTasks.Count < 2)
            return Ok(new { hasSuggestion = false });

        var avgRatio = completedTasks.Average(t => (double)t.ActualHours!.Value / (double)t.EstimatedHours!.Value);
        var suggested = estimatedHours.HasValue ? Math.Round((double)estimatedHours.Value * avgRatio, 1) : (double?)null;

        return Ok(new
        {
            hasSuggestion = true,
            adjustmentFactor = Math.Round(avgRatio, 2),
            suggestedHours = suggested,
            sampleSize = completedTasks.Count
        });
    }

    // ML Learning: insights per course
    [HttpGet("learning-insights")]
    public async Task<IActionResult> GetLearningInsights()
    {
        var email = GetEmail();
        var completedTasks = await _db.Tasks
            .Include(t => t.Course)
            .Where(t => t.Email == email && t.IsCompleted && t.ActualHours.HasValue && t.EstimatedHours.HasValue && t.EstimatedHours > 0)
            .ToListAsync();

        var insights = completedTasks
            .GroupBy(t => new { t.CourseId, t.Course.CourseName })
            .Select(g => new
            {
                courseId = g.Key.CourseId,
                courseName = g.Key.CourseName,
                taskCount = g.Count(),
                avgEstimated = Math.Round(g.Average(t => (double)t.EstimatedHours!.Value), 1),
                avgActual = Math.Round(g.Average(t => (double)t.ActualHours!.Value), 1),
                accuracy = Math.Round(g.Average(t => Math.Min((double)t.EstimatedHours!.Value, (double)t.ActualHours!.Value) /
                    Math.Max((double)t.EstimatedHours!.Value, (double)t.ActualHours!.Value)) * 100, 0)
            })
            .OrderBy(i => i.courseName)
            .ToList();

        return Ok(insights);
    }

    private static TaskDto BuildTaskDto(StudentTask t)
    {
        var taskEvents = t.TaskEvents?.Where(te => te.Status == "Scheduled" || te.Status == "Partial").ToList()
            ?? new List<TaskEvent>();

        string schedulingStatus;
        if (t.IsCompleted)
            schedulingStatus = "Completed";
        else if (!taskEvents.Any())
            schedulingStatus = "Unscheduled";
        else if (taskEvents.Any(te => te.Status == "Partial"))
            schedulingStatus = "Partial";
        else
            schedulingStatus = "Scheduled";

        var subTasks = t.SubTasks?.ToList() ?? new List<StudentTask>();
        var completedSubCount = subTasks.Count(s => s.IsCompleted);

        // Shared task info
        var isShared = t.SharedTask != null;
        string? sharedStatus = t.SharedTask?.SharedStatus;
        string? sharedWithName = null;
        if (t.SharedTask?.Members != null)
        {
            var otherMember = t.SharedTask.Members.FirstOrDefault(m => m.Email != t.Email);
            if (otherMember?.User != null)
                sharedWithName = $"{otherMember.User.FirstName} {otherMember.User.LastName}";
        }

        return new TaskDto
        {
            TaskId = t.TaskId,
            CourseId = t.CourseId,
            CourseName = t.Course?.CourseName ?? "",
            Title = t.Title,
            Type = t.Type,
            EstimatedHours = t.EstimatedHours,
            DueDate = t.DueDate,
            IsCompleted = t.IsCompleted,
            Priority = t.Priority,
            ActualHours = t.ActualHours,
            ParentTaskId = t.ParentTaskId,
            SubTasks = subTasks.Any() ? subTasks.Select(s => BuildTaskDto(s)).ToList() : null,
            SubTaskCount = subTasks.Count,
            CompletedSubTaskCount = completedSubCount,
            SubTaskProgress = subTasks.Count > 0 ? Math.Round((double)completedSubCount / subTasks.Count * 100, 0) : 0,
            IsShared = isShared,
            SharedStatus = sharedStatus,
            SharedWithName = sharedWithName,
            ScheduledDate = taskEvents.OrderBy(te => te.From).FirstOrDefault()?.From,
            SchedulingStatus = schedulingStatus,
            ScheduledSlots = taskEvents.Select(te => new TaskSlotDto
            {
                From = te.From,
                To = te.To
            }).OrderBy(s => s.From).ToList(),
            AllowSplitting = t.AllowSplitting,
            IsManuallyPinned = t.IsManuallyPinned
        };
    }
}
