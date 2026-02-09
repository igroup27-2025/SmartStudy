using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly SmartStudyDbContext _db;

    public EventsController(SmartStudyDbContext db) => _db = db;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var email = GetEmail();
        var query = _db.Events.Where(e => e.Email == email);

        if (from.HasValue) query = query.Where(e => e.To >= from.Value);
        if (to.HasValue) query = query.Where(e => e.From <= to.Value);

        var events = await query.OrderBy(e => e.From).ToListAsync();
        var result = new List<EventDto>();

        foreach (var evt in events)
        {
            var dto = new EventDto
            {
                EventId = evt.EventId,
                From = evt.From,
                To = evt.To,
                Recurring = evt.Recurring
            };

            var classEvent = await _db.ClassEvents.Include(ce => ce.Course)
                .FirstOrDefaultAsync(ce => ce.EventId == evt.EventId);
            if (classEvent != null)
            {
                dto.EventType = "class";
                dto.CourseId = classEvent.CourseId;
                dto.CourseName = classEvent.Course.CourseName;
                dto.Location = classEvent.Location;
                dto.Duration = classEvent.Duration;
                result.Add(dto);
                continue;
            }

            var taskEvent = await _db.TaskEvents.Include(te => te.StudentTask)
                .FirstOrDefaultAsync(te => te.EventId == evt.EventId);
            if (taskEvent != null)
            {
                dto.EventType = "task";
                dto.TaskId = taskEvent.TaskId;
                dto.TaskTitle = taskEvent.StudentTask.Title;
                dto.Priority = taskEvent.Priority;
                dto.ActualHours = taskEvent.ActualHours;
                dto.Status = taskEvent.Status;
                result.Add(dto);
                continue;
            }

            var workEvent = await _db.WorkEvents.FirstOrDefaultAsync(we => we.EventId == evt.EventId);
            if (workEvent != null)
            {
                dto.EventType = "work";
                dto.TravelTime = workEvent.TravelTime;
                dto.WorkPlace = workEvent.WorkPlace;
                result.Add(dto);
                continue;
            }

            var personalEvent = await _db.PersonalEvents.FirstOrDefaultAsync(pe => pe.EventId == evt.EventId);
            if (personalEvent != null)
            {
                dto.EventType = "personal";
                dto.Type = personalEvent.Type;
                dto.Description = personalEvent.Description;
                result.Add(dto);
                continue;
            }

            dto.EventType = "unknown";
            result.Add(dto);
        }

        return Ok(result);
    }

    [HttpPost("class")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassEventDto dto)
    {
        var email = GetEmail();
        var evt = new ClassEvent
        {
            Email = email,
            From = dto.From,
            To = dto.To,
            Recurring = dto.Recurring,
            CourseId = dto.CourseId,
            Location = dto.Location,
            Duration = dto.Duration
        };
        _db.ClassEvents.Add(evt);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { }, new { evt.EventId, eventType = "class" });
    }

    [HttpPost("task")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskEventDto dto)
    {
        var email = GetEmail();
        var evt = new TaskEvent
        {
            Email = email,
            From = dto.From,
            To = dto.To,
            Recurring = dto.Recurring,
            TaskId = dto.TaskId,
            Priority = dto.Priority,
            Status = dto.Status ?? "Scheduled"
        };
        _db.TaskEvents.Add(evt);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { }, new { evt.EventId, eventType = "task" });
    }

    [HttpPost("work")]
    public async Task<IActionResult> CreateWork([FromBody] CreateWorkEventDto dto)
    {
        var email = GetEmail();
        var evt = new WorkEvent
        {
            Email = email,
            From = dto.From,
            To = dto.To,
            Recurring = dto.Recurring,
            TravelTime = dto.TravelTime,
            WorkPlace = dto.WorkPlace
        };
        _db.WorkEvents.Add(evt);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { }, new { evt.EventId, eventType = "work" });
    }

    [HttpPost("personal")]
    public async Task<IActionResult> CreatePersonal([FromBody] CreatePersonalEventDto dto)
    {
        var email = GetEmail();
        var evt = new PersonalEvent
        {
            Email = email,
            From = dto.From,
            To = dto.To,
            Recurring = dto.Recurring,
            Type = dto.Type,
            Description = dto.Description
        };
        _db.PersonalEvents.Add(evt);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { }, new { evt.EventId, eventType = "personal" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var evt = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id && e.Email == email);
        if (evt == null) return NotFound();

        _db.Events.Remove(evt);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
