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
        var query = _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents).Where(t => t.Email == email);

        if (courseId.HasValue)
            query = query.Where(t => t.CourseId == courseId.Value);
        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        var tasks = await query
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ToListAsync();

        var dtos = tasks.Select(t => BuildTaskDto(t)).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents)
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
            Priority = dto.Priority,
            Email = email,
            IsCompleted = false
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        // Trigger scheduling for all tasks
        await _scheduling.ScheduleAllTasksAsync(email);

        // Reload with scheduling info
        var reloaded = await _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents)
            .FirstAsync(t => t.TaskId == task.TaskId);

        return CreatedAtAction(nameof(Get), new { id = task.TaskId }, BuildTaskDto(reloaded));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);

        if (task == null) return NotFound();

        if (dto.CourseId.HasValue) task.CourseId = dto.CourseId.Value;
        if (dto.Title != null) task.Title = dto.Title;
        if (dto.Type != null) task.Type = dto.Type;
        if (dto.EstimatedHours.HasValue) task.EstimatedHours = dto.EstimatedHours;
        if (dto.DueDate.HasValue) task.DueDate = dto.DueDate;
        if (dto.Priority != null) task.Priority = dto.Priority;
        if (dto.IsCompleted.HasValue) task.IsCompleted = dto.IsCompleted.Value;

        await _db.SaveChangesAsync();

        // Trigger rescheduling
        await _scheduling.ScheduleAllTasksAsync(email);

        // Reload with scheduling info
        var reloaded = await _db.Tasks.Include(t => t.Course).Include(t => t.TaskEvents)
            .FirstAsync(t => t.TaskId == task.TaskId);

        return Ok(BuildTaskDto(reloaded));
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.TaskEvents)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);
        if (task == null) return NotFound();

        task.IsCompleted = !task.IsCompleted;

        // Remove task events when completing
        if (task.IsCompleted && task.TaskEvents.Any())
        {
            var eventIds = task.TaskEvents.Select(te => te.EventId).ToList();
            var events = await _db.Events.Where(e => eventIds.Contains(e.EventId)).ToListAsync();
            _db.Events.RemoveRange(events);
        }

        await _db.SaveChangesAsync();

        // Reschedule remaining tasks
        await _scheduling.ScheduleAllTasksAsync(email);

        return Ok(new { task.TaskId, task.IsCompleted });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);
        if (task == null) return NotFound();

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();

        // Reschedule remaining tasks (cascade already deleted TaskEvents)
        await _scheduling.ScheduleAllTasksAsync(email);

        return NoContent();
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
            ScheduledDate = taskEvents.OrderBy(te => te.From).FirstOrDefault()?.From,
            SchedulingStatus = schedulingStatus,
            ScheduledSlots = taskEvents.Select(te => new TaskSlotDto
            {
                From = te.From,
                To = te.To
            }).OrderBy(s => s.From).ToList()
        };
    }
}
