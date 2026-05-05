using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    private static string? StripGcalTag(string? desc) => TextHelpers.StripGcalTag(desc);

    [HttpGet]
    public IActionResult GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var email = GetEmail();
        var events = Event.GetAllTypedEventsInRange(email, from, to);

        var result = new List<EventDto>();

        foreach (var evt in events)
        {
            if ((!from.HasValue || evt.To >= from.Value) && (!to.HasValue || evt.From <= to.Value))
                result.Add(BuildDto(evt));

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
                dto.IsShared = evt.IsShared;
                dto.SharedStatus = evt.SharedStatus;
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
    public IActionResult CreateClass([FromBody] CreateClassEventDto dto)
    {
        var email = GetEmail();
        var eventId = Event.CreateClass(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.CourseId, dto.Location, dto.Duration);

        var conflictsBefore = Event.CountConflictingTaskEvents(email, dto.From, dto.To, eventId);
        StudentTask.ScheduleAll(email);
        var conflictsAfter = Event.CountConflictingTaskEvents(email, dto.From, dto.To, eventId);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            EventId = eventId,
            eventType = "class",
            conflictsDetected = conflictsBefore,
            conflictsAutoResolved = conflictsBefore > 0 && conflictsAfter == 0
        });
    }

    [HttpPost("task")]
    public IActionResult CreateTask([FromBody] CreateTaskEventDto dto)
    {
        var email = GetEmail();
        var eventId = Event.CreateTaskEvent(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.TaskId, dto.Priority, dto.Status ?? "Scheduled");
        return CreatedAtAction(nameof(GetAll), new { }, new { EventId = eventId, eventType = "task" });
    }

    [HttpPost("work")]
    public IActionResult CreateWork([FromBody] CreateWorkEventDto dto)
    {
        var email = GetEmail();
        var eventId = Event.CreateWork(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.WorkPlace, dto.TravelTime);

        var conflictsBefore = Event.CountConflictingTaskEvents(email, dto.From, dto.To, eventId);
        StudentTask.ScheduleAll(email);
        var conflictsAfter = Event.CountConflictingTaskEvents(email, dto.From, dto.To, eventId);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            EventId = eventId,
            eventType = "work",
            conflictsDetected = conflictsBefore,
            conflictsAutoResolved = conflictsBefore > 0 && conflictsAfter == 0
        });
    }

    [HttpPost("personal")]
    public IActionResult CreatePersonal([FromBody] CreatePersonalEventDto dto)
    {
        var email = GetEmail();
        var eventId = Event.CreatePersonal(email, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.Type, dto.Description);

        var conflictsBefore = Event.CountConflictingTaskEvents(email, dto.From, dto.To, eventId);
        StudentTask.ScheduleAll(email);
        var conflictsAfter = Event.CountConflictingTaskEvents(email, dto.From, dto.To, eventId);

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            EventId = eventId,
            eventType = "personal",
            conflictsDetected = conflictsBefore,
            conflictsAutoResolved = conflictsBefore > 0 && conflictsAfter == 0
        });
    }

    [HttpPut("class/{id}")]
    public IActionResult UpdateClass(int id, [FromBody] CreateClassEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = Event.GetOwnerEmail(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        Event.UpdateClass(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.CourseId, dto.Location, dto.Duration);
        StudentTask.ScheduleAll(email);
        return Ok(new { EventId = id, eventType = "class" });
    }

    [HttpPut("work/{id}")]
    public IActionResult UpdateWork(int id, [FromBody] CreateWorkEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = Event.GetOwnerEmail(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        Event.UpdateWork(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.WorkPlace, dto.TravelTime);
        StudentTask.ScheduleAll(email);
        return Ok(new { EventId = id, eventType = "work" });
    }

    [HttpPut("task/{id}")]
    public IActionResult UpdateTask(int id, [FromBody] CreateTaskEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = Event.GetOwnerEmail(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        var oldTimes = Event.GetEventTimeRange(id);

        Event.UpdateTaskEvent(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.TaskId, dto.Priority, dto.Status);
        Event.PinTask(dto.TaskId);

        bool partnerSynced = false;
        if (oldTimes.HasValue)
        {
            var partnerTaskId = Event.GetSharedPartnerTaskId(dto.TaskId);
            if (partnerTaskId.HasValue)
            {
                var partnerEventId = Event.SyncSharedTaskEventMove(
                    id, partnerTaskId.Value,
                    oldTimes.Value.From, oldTimes.Value.To,
                    dto.From, dto.To);
                partnerSynced = partnerEventId.HasValue;
                if (partnerSynced) Event.PinTask(partnerTaskId.Value);
            }
        }

        return Ok(new { EventId = id, eventType = "task", isManuallyPinned = true, partnerSynced });
    }

    [HttpPut("personal/{id}")]
    public IActionResult UpdatePersonal(int id, [FromBody] CreatePersonalEventDto dto)
    {
        var email = GetEmail();
        var ownerEmail = Event.GetOwnerEmail(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        Event.UpdatePersonal(id, dto.From, dto.To, dto.Recurring, dto.RecurrenceEndDate, dto.Type, dto.Description);
        StudentTask.ScheduleAll(email);
        return Ok(new { EventId = id, eventType = "personal" });
    }

    [HttpPost("check-conflicts")]
    public IActionResult CheckConflicts([FromBody] CheckConflictDto dto)
    {
        var email = GetEmail();

        var events = Event.GetConflicting(email, dto.From, dto.To, dto.ExcludeEventId);

        var conflicts = new List<EventDto>();

        foreach (var evt in events)
        {
            if (dto.ExcludeEventId.HasValue && evt.EventId == dto.ExcludeEventId.Value) continue;

            if (evt.From < dto.To && evt.To > dto.From)
            {
                conflicts.Add(BuildDto(evt));
            }

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
    public IActionResult ChangeType(int id, [FromBody] ChangeEventTypeDto dto)
    {
        var email = GetEmail();
        var ownerEmail = Event.GetOwnerEmail(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        var currentType = Event.GetSubtype(id);

        if (currentType == "class" || currentType == "task")
            return BadRequest(new { message = "Class and Task events cannot change type" });

        var newType = dto.NewType?.ToLower();
        if (newType != "work" && newType != "personal")
            return BadRequest(new { message = "New type must be 'work' or 'personal'" });

        if (currentType == newType)
            return Ok(new { EventId = id, eventType = newType, message = "Type unchanged" });

        Event.ChangeType(id, currentType, newType, dto.WorkPlace, dto.TravelTime, dto.PersonalType, dto.Description);
        StudentTask.ScheduleAll(email);
        return Ok(new { EventId = id, eventType = newType });
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var email = GetEmail();
        var ownerEmail = Event.GetOwnerEmail(id);
        if (ownerEmail == null || ownerEmail != email) return NotFound();

        Event.Delete(id);
        try { StudentTask.ScheduleAll(email); } catch { }
        return NoContent();
    }
}
