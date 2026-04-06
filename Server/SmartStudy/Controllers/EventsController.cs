using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly DBservices _db;
    private readonly SchedulingService _scheduling;

    public EventsController(DBservices db, SchedulingService scheduling)
    {
        _db = db;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    private static string? StripGcalTag(string? desc) =>
        desc == null ? null : System.Text.RegularExpressions.Regex.Replace(desc, @"\s*\[gcal:[^\]]*\]", "").Trim();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var email = GetEmail();
        var events = await _db.GetAllTypedEventsInRangeAsync(email, from, to);

        var result = new List<EventDto>();

        foreach (var evt in events)
        {
            // Add the original event (if it falls in range)
            if ((!from.HasValue || evt.To >= from.Value) && (!to.HasValue || evt.From <= to.Value))
                result.Add(BuildDto(evt));

            // Expand recurring events into weekly copies within the range
            if (evt.Recurring && from.HasValue && to.HasValue)
            {
                var duration = evt.To - evt.From;
                var endLimit = evt.RecurrenceEndDate.HasValue
                    ? (to.Value < evt.RecurrenceEndDate.Value ? to.Value : evt.RecurrenceEndDate.Value)
                    : to.Value;
                var nextFrom = evt.From.AddDays(7);
                while (nextFrom <= endLimit)
                {
                    var nextTo = nextFrom.Add(duration);
                    if (nextTo >= from.Value)
                    {
                        var virtualDto = BuildDto(evt);
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

    private static EventDto BuildDto(TypedEvent evt)
    {
        var dto = new EventDto
        {
            EventId = evt.EventId,
            From = evt.From,
            To = evt.To,
            Recurring = evt.Recurring,
            RecurrenceEndDate = evt.RecurrenceEndDate
        };

        switch (evt.EventType)
        {
            case "class":
                dto.EventType = "class";
                dto.CourseId = evt.CourseId;
                dto.CourseName = evt.CourseName;
                dto.Location = evt.Location;
                dto.Duration = evt.Duration;
                break;
            case "task":
                dto.EventType = "task";
                dto.TaskId = evt.TaskId;
                dto.TaskTitle = evt.TaskTitle;
                dto.Priority = evt.Priority;
                dto.ActualHours = evt.ActualHours;
                dto.Status = evt.Status;
                dto.IsManuallyPinned = evt.IsManuallyPinned;
                break;
            case "work":
                dto.EventType = "work";
                dto.TravelTime = evt.TravelTime;
                dto.WorkPlace = evt.WorkPlace;
                break;
            case "personal":
                dto.EventType = "personal";
                dto.Type = evt.Type;
                dto.Description = StripGcalTag(evt.Description);
                break;
            default:
                dto.EventType = "unknown";
                break;
        }

        return dto;
    }

    [HttpPost("class")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassEventDto dto)
    {
        var email = GetEmail();
        var eventId = await _db.CreateClassEventAsync(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.CourseId, dto.Location, dto.Duration);

        var conflictsBefore = await _db.CountConflictingTaskEventsAsync(email, dto.From, dto.To, eventId);
        await _scheduling.ScheduleAllTasksAsync(email);
        var conflictsAfter = await _db.CountConflictingTaskEventsAsync(email, dto.From, dto.To, eventId);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            EventId = eventId,
            eventType = "class",
            conflictsDetected = conflictsBefore,
            conflictsAutoResolved = conflictsBefore > 0 && conflictsAfter == 0
        });
    }

    [HttpPost("task")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskEventDto dto)
    {
        var email = GetEmail();
        var eventId = await _db.CreateTaskEventAsync(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.TaskId, dto.Priority, dto.Status ?? "Scheduled");
        return CreatedAtAction(nameof(GetAll), new { }, new { EventId = eventId, eventType = "task" });
    }

    [HttpPost("work")]
    public async Task<IActionResult> CreateWork([FromBody] CreateWorkEventDto dto)
    {
        var email = GetEmail();
        var eventId = await _db.CreateWorkEventAsync(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.WorkPlace, dto.TravelTime);

        var conflictsBefore = await _db.CountConflictingTaskEventsAsync(email, dto.From, dto.To, eventId);
        await _scheduling.ScheduleAllTasksAsync(email);
        var conflictsAfter = await _db.CountConflictingTaskEventsAsync(email, dto.From, dto.To, eventId);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            EventId = eventId,
            eventType = "work",
            conflictsDetected = conflictsBefore,
            conflictsAutoResolved = conflictsBefore > 0 && conflictsAfter == 0
        });
    }

    [HttpPost("personal")]
    public async Task<IActionResult> CreatePersonal([FromBody] CreatePersonalEventDto dto)
    {
        var email = GetEmail();
        var eventId = await _db.CreatePersonalEventAsync(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.Type, dto.Description);

        var conflictsBefore = await _db.CountConflictingTaskEventsAsync(email, dto.From, dto.To, eventId);
        await _scheduling.ScheduleAllTasksAsync(email);
        var conflictsAfter = await _db.CountConflictingTaskEventsAsync(email, dto.From, dto.To, eventId);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            EventId = eventId,
            eventType = "personal",
            conflictsDetected = conflictsBefore,
            conflictsAutoResolved = conflictsBefore > 0 && conflictsAfter == 0
        });
    }

    [HttpPut("class/{id}")]
    public async Task<IActionResult> UpdateClass(int id, [FromBody] CreateClassEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = await _db.GetEventOwnerEmailAsync(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        await _db.UpdateClassEventAsync(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.CourseId, dto.Location, dto.Duration);
        await _scheduling.ScheduleAllTasksAsync(email);
        return Ok(new { EventId = id, eventType = "class" });
    }

    [HttpPut("work/{id}")]
    public async Task<IActionResult> UpdateWork(int id, [FromBody] CreateWorkEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = await _db.GetEventOwnerEmailAsync(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        await _db.UpdateWorkEventAsync(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.WorkPlace, dto.TravelTime);
        await _scheduling.ScheduleAllTasksAsync(email);
        return Ok(new { EventId = id, eventType = "work" });
    }

    [HttpPut("task/{id}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] CreateTaskEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = await _db.GetEventOwnerEmailAsync(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        await _db.UpdateTaskEventAsync(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.TaskId, dto.Priority, dto.Status);

        // Pin the parent task so it's excluded from auto-scheduling
        await _db.PinTaskAsync(dto.TaskId);

        return Ok(new { EventId = id, eventType = "task", isManuallyPinned = true });
    }

    [HttpPut("personal/{id}")]
    public async Task<IActionResult> UpdatePersonal(int id, [FromBody] CreatePersonalEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = await _db.GetEventOwnerEmailAsync(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        await _db.UpdatePersonalEventAsync(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.Type, dto.Description);
        await _scheduling.ScheduleAllTasksAsync(email);
        return Ok(new { EventId = id, eventType = "personal" });
    }

    [HttpPost("check-conflicts")]
    public async Task<IActionResult> CheckConflicts([FromBody] CheckConflictDto dto)
    {
        var email = GetEmail();

        // Get all events that could conflict (including recurring ones)
        var events = await _db.GetConflictingEventsAsync(email, dto.From, dto.To, dto.ExcludeEventId);

        var conflicts = new List<EventDto>();

        foreach (var evt in events)
        {
            // Skip the event being edited
            if (dto.ExcludeEventId.HasValue && evt.EventId == dto.ExcludeEventId.Value) continue;

            // Check direct overlap
            if (evt.From < dto.To && evt.To > dto.From)
            {
                conflicts.Add(BuildDto(evt));
            }

            // Check recurring copies
            if (evt.Recurring)
            {
                var duration = evt.To - evt.From;
                var loopLimit = dto.To.AddDays(7);
                if (evt.RecurrenceEndDate.HasValue && evt.RecurrenceEndDate.Value < loopLimit)
                    loopLimit = evt.RecurrenceEndDate.Value;
                var nextFrom = evt.From.AddDays(7);
                for (int i = 0; i < 52 && nextFrom < loopLimit; i++)
                {
                    var nextTo = nextFrom.Add(duration);
                    if (nextFrom < dto.To && nextTo > dto.From)
                    {
                        if (dto.ExcludeEventId.HasValue && evt.EventId == dto.ExcludeEventId.Value) continue;
                        var virtualDto = BuildDto(evt);
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

    [HttpPut("{id}/change-type")]
    public async Task<IActionResult> ChangeType(int id, [FromBody] ChangeEventTypeDto dto)
    {
        var email = GetEmail();
        var ownerEmail = await _db.GetEventOwnerEmailAsync(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        // Determine current type
        var currentType = await _db.GetEventSubtypeAsync(id);

        // Class and Task events truly cannot change type
        if (currentType == "class" || currentType == "task")
            return BadRequest(new { message = "Class and Task events cannot change type" });

        var newType = dto.NewType?.ToLower();
        if (newType != "work" && newType != "personal")
            return BadRequest(new { message = "New type must be 'work' or 'personal'" });

        if (currentType == newType)
            return Ok(new { EventId = id, eventType = newType, message = "Type unchanged" });

        await _db.ChangeEventTypeAsync(id, currentType, newType, dto.WorkPlace, dto.TravelTime, dto.PersonalType, dto.Description);
        await _scheduling.ScheduleAllTasksAsync(email);
        return Ok(new { EventId = id, eventType = newType });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var ownerEmail = await _db.GetEventOwnerEmailAsync(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        await _db.DeleteEventAsync(id);
        await _scheduling.ScheduleAllTasksAsync(email);
        return NoContent();
    }
}
