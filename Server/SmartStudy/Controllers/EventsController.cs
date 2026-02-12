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

        // For recurring expansion, also fetch recurring events that started before the range
        if (from.HasValue && to.HasValue)
            query = _db.Events.Where(e => e.Email == email &&
                ((e.To >= from.Value && e.From <= to.Value) || e.Recurring));
        else
        {
            if (from.HasValue) query = query.Where(e => e.To >= from.Value);
            if (to.HasValue) query = query.Where(e => e.From <= to.Value);
        }

        var events = await query.OrderBy(e => e.From).ToListAsync();

        // Pre-load all subtype data for these events
        var eventIds = events.Select(e => e.EventId).ToList();
        var classEvents = await _db.ClassEvents.Include(ce => ce.Course)
            .Where(ce => eventIds.Contains(ce.EventId)).ToDictionaryAsync(ce => ce.EventId);
        var taskEvents = await _db.TaskEvents.Include(te => te.StudentTask)
            .Where(te => eventIds.Contains(te.EventId)).ToDictionaryAsync(te => te.EventId);
        var workEvents = await _db.WorkEvents
            .Where(we => eventIds.Contains(we.EventId)).ToDictionaryAsync(we => we.EventId);
        var personalEvents = await _db.PersonalEvents
            .Where(pe => eventIds.Contains(pe.EventId)).ToDictionaryAsync(pe => pe.EventId);

        var result = new List<EventDto>();

        foreach (var evt in events)
        {
            // Add the original event (if it falls in range)
            if ((!from.HasValue || evt.To >= from.Value) && (!to.HasValue || evt.From <= to.Value))
                result.Add(BuildDto(evt, classEvents, taskEvents, workEvents, personalEvents));

            // Expand recurring events into weekly copies within the range
            if (evt.Recurring && from.HasValue && to.HasValue)
            {
                var duration = evt.To - evt.From;
                var nextFrom = evt.From.AddDays(7);
                while (nextFrom <= to.Value)
                {
                    var nextTo = nextFrom.Add(duration);
                    if (nextTo >= from.Value)
                    {
                        var virtualDto = BuildDto(evt, classEvents, taskEvents, workEvents, personalEvents);
                        virtualDto.EventId = evt.EventId;
                        virtualDto.From = nextFrom;
                        virtualDto.To = nextTo;
                        result.Add(virtualDto);
                    }
                    nextFrom = nextFrom.AddDays(7);
                }
            }
        }

        return Ok(result.OrderBy(e => e.From));
    }

    private static EventDto BuildDto(
        Event evt,
        Dictionary<int, ClassEvent> classEvents,
        Dictionary<int, TaskEvent> taskEvents,
        Dictionary<int, WorkEvent> workEvents,
        Dictionary<int, PersonalEvent> personalEvents)
    {
        var dto = new EventDto
        {
            EventId = evt.EventId,
            From = evt.From,
            To = evt.To,
            Recurring = evt.Recurring
        };

        if (classEvents.TryGetValue(evt.EventId, out var ce))
        {
            dto.EventType = "class";
            dto.CourseId = ce.CourseId;
            dto.CourseName = ce.Course.CourseName;
            dto.Location = ce.Location;
            dto.Duration = ce.Duration;
        }
        else if (taskEvents.TryGetValue(evt.EventId, out var te))
        {
            dto.EventType = "task";
            dto.TaskId = te.TaskId;
            dto.TaskTitle = te.StudentTask.Title;
            dto.Priority = te.Priority;
            dto.ActualHours = te.ActualHours;
            dto.Status = te.Status;
        }
        else if (workEvents.TryGetValue(evt.EventId, out var we))
        {
            dto.EventType = "work";
            dto.TravelTime = we.TravelTime;
            dto.WorkPlace = we.WorkPlace;
        }
        else if (personalEvents.TryGetValue(evt.EventId, out var pe))
        {
            dto.EventType = "personal";
            dto.Type = pe.Type;
            dto.Description = pe.Description;
        }
        else
        {
            dto.EventType = "unknown";
        }

        return dto;
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
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
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
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
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
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
        return CreatedAtAction(nameof(GetAll), new { }, new { evt.EventId, eventType = "personal" });
    }

    [HttpPut("class/{id}")]
    public async Task<IActionResult> UpdateClass(int id, [FromBody] CreateClassEventDto dto)
    {
        var email = GetEmail();
        var evt = await _db.ClassEvents.FirstOrDefaultAsync(e => e.EventId == id && e.Email == email);
        if (evt == null) return NotFound();

        evt.From = dto.From;
        evt.To = dto.To;
        evt.Recurring = dto.Recurring;
        evt.CourseId = dto.CourseId;
        evt.Location = dto.Location;
        evt.Duration = dto.Duration;
        await _db.SaveChangesAsync();
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
        return Ok(new { evt.EventId, eventType = "class" });
    }

    [HttpPut("work/{id}")]
    public async Task<IActionResult> UpdateWork(int id, [FromBody] CreateWorkEventDto dto)
    {
        var email = GetEmail();
        var evt = await _db.WorkEvents.FirstOrDefaultAsync(e => e.EventId == id && e.Email == email);
        if (evt == null) return NotFound();

        evt.From = dto.From;
        evt.To = dto.To;
        evt.Recurring = dto.Recurring;
        evt.TravelTime = dto.TravelTime;
        evt.WorkPlace = dto.WorkPlace;
        await _db.SaveChangesAsync();
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
        return Ok(new { evt.EventId, eventType = "work" });
    }

    [HttpPut("personal/{id}")]
    public async Task<IActionResult> UpdatePersonal(int id, [FromBody] CreatePersonalEventDto dto)
    {
        var email = GetEmail();
        var evt = await _db.PersonalEvents.FirstOrDefaultAsync(e => e.EventId == id && e.Email == email);
        if (evt == null) return NotFound();

        evt.From = dto.From;
        evt.To = dto.To;
        evt.Recurring = dto.Recurring;
        evt.Type = dto.Type;
        evt.Description = dto.Description;
        await _db.SaveChangesAsync();
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
        return Ok(new { evt.EventId, eventType = "personal" });
    }

    [HttpPost("check-conflicts")]
    public async Task<IActionResult> CheckConflicts([FromBody] CheckConflictDto dto)
    {
        var email = GetEmail();

        // Get all events in the time range (including recurring expansion)
        var events = await _db.Events
            .Where(e => e.Email == email &&
                ((e.To > dto.From && e.From < dto.To) || e.Recurring))
            .OrderBy(e => e.From)
            .ToListAsync();

        var eventIds = events.Select(e => e.EventId).ToList();
        var classEvents = await _db.ClassEvents.Include(ce => ce.Course)
            .Where(ce => eventIds.Contains(ce.EventId)).ToDictionaryAsync(ce => ce.EventId);
        var taskEvents = await _db.TaskEvents.Include(te => te.StudentTask)
            .Where(te => eventIds.Contains(te.EventId)).ToDictionaryAsync(te => te.EventId);
        var workEvents = await _db.WorkEvents
            .Where(we => eventIds.Contains(we.EventId)).ToDictionaryAsync(we => we.EventId);
        var personalEvents = await _db.PersonalEvents
            .Where(pe => eventIds.Contains(pe.EventId)).ToDictionaryAsync(pe => pe.EventId);

        var conflicts = new List<EventDto>();

        foreach (var evt in events)
        {
            // Skip the event being edited
            if (dto.ExcludeEventId.HasValue && evt.EventId == dto.ExcludeEventId.Value) continue;

            // Check direct overlap
            if (evt.From < dto.To && evt.To > dto.From)
            {
                conflicts.Add(BuildDto(evt, classEvents, taskEvents, workEvents, personalEvents));
            }

            // Check recurring copies
            if (evt.Recurring)
            {
                var duration = evt.To - evt.From;
                var nextFrom = evt.From.AddDays(7);
                for (int i = 0; i < 52 && nextFrom < dto.To.AddDays(7); i++)
                {
                    var nextTo = nextFrom.Add(duration);
                    if (nextFrom < dto.To && nextTo > dto.From)
                    {
                        if (dto.ExcludeEventId.HasValue && evt.EventId == dto.ExcludeEventId.Value) continue;
                        var virtualDto = BuildDto(evt, classEvents, taskEvents, workEvents, personalEvents);
                        virtualDto.From = nextFrom;
                        virtualDto.To = nextTo;
                        conflicts.Add(virtualDto);
                    }
                    nextFrom = nextFrom.AddDays(7);
                }
            }
        }

        return Ok(new { hasConflicts = conflicts.Any(), conflicts });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var evt = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id && e.Email == email);
        if (evt == null) return NotFound();

        _db.Events.Remove(evt);
        await _db.SaveChangesAsync();
        await new SchedulingService(_db).ScheduleAllTasksAsync(email);
        return NoContent();
    }
}
