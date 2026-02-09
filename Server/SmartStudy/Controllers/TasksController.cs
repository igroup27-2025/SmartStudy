using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly SmartStudyDbContext _db;

    public TasksController(SmartStudyDbContext db) => _db = db;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId, [FromQuery] bool? completed)
    {
        var email = GetEmail();
        var query = _db.Tasks.Include(t => t.Course).Where(t => t.Email == email);

        if (courseId.HasValue)
            query = query.Where(t => t.CourseId == courseId.Value);
        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        var tasks = await query
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .Select(t => new TaskDto
            {
                TaskId = t.TaskId,
                CourseId = t.CourseId,
                CourseName = t.Course.CourseName,
                Title = t.Title,
                Type = t.Type,
                EstimatedHours = t.EstimatedHours,
                DueDate = t.DueDate,
                IsCompleted = t.IsCompleted,
                Priority = t.Priority
            })
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.Course)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);

        if (task == null) return NotFound();

        return Ok(new TaskDto
        {
            TaskId = task.TaskId,
            CourseId = task.CourseId,
            CourseName = task.Course.CourseName,
            Title = task.Title,
            Type = task.Type,
            EstimatedHours = task.EstimatedHours,
            DueDate = task.DueDate,
            IsCompleted = task.IsCompleted,
            Priority = task.Priority
        });
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

        var course = await _db.Courses.FindAsync(dto.CourseId);
        return CreatedAtAction(nameof(Get), new { id = task.TaskId }, new TaskDto
        {
            TaskId = task.TaskId,
            CourseId = task.CourseId,
            CourseName = course?.CourseName ?? "",
            Title = task.Title,
            Type = task.Type,
            EstimatedHours = task.EstimatedHours,
            DueDate = task.DueDate,
            IsCompleted = task.IsCompleted,
            Priority = task.Priority
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var email = GetEmail();
        var task = await _db.Tasks.Include(t => t.Course)
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

        return Ok(new TaskDto
        {
            TaskId = task.TaskId,
            CourseId = task.CourseId,
            CourseName = task.Course.CourseName,
            Title = task.Title,
            Type = task.Type,
            EstimatedHours = task.EstimatedHours,
            DueDate = task.DueDate,
            IsCompleted = task.IsCompleted,
            Priority = task.Priority
        });
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var email = GetEmail();
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.TaskId == id && t.Email == email);
        if (task == null) return NotFound();

        task.IsCompleted = !task.IsCompleted;
        await _db.SaveChangesAsync();

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
        return NoContent();
    }
}
