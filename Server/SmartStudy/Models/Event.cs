using SmartStudy.DAL;

namespace SmartStudy.Models;

// Base entity for all calendar events; subclassed by Class/Task/Work/Personal events.
public class Event
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }

    // Navigation property
    public User User { get; set; } = null!;

    // ───── EventsBLL methods folded in ──────────────────────────────────

    // Returns every event in the user's calendar within an optional date window, with subtype data flattened.
    public static List<TypedEvent> GetAllTypedEventsInRange(string email, DateTime? from, DateTime? to)
    {
        DBservices db = new DBservices();
        return db.GetAllTypedEventsInRange(email, from, to);
    }

    // Inserts a class event tied to a course and returns the new event ID.
    public static int CreateClass(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        DBservices db = new DBservices();
        return db.CreateClassEvent(email, from, to, recurring, recurrenceEndDate, courseId, location, duration);
    }

    // Inserts a task study-block event linking back to a task, returning the new event ID.
    public static int CreateTaskEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        DBservices db = new DBservices();
        return db.CreateTaskEvent(email, from, to, recurring, recurrenceEndDate, taskId, priority, status);
    }

    // Inserts a work-shift event and returns the new event ID.
    public static int CreateWork(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime = null)
    {
        DBservices db = new DBservices();
        return db.CreateWorkEvent(email, from, to, recurring, recurrenceEndDate, workPlace, travelTime);
    }

    // Inserts a personal event (sleep/meal/etc.) and returns the new event ID.
    public static int CreatePersonal(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        DBservices db = new DBservices();
        return db.CreatePersonalEvent(email, from, to, recurring, recurrenceEndDate, type, description);
    }

    // Updates a class event's fields.
    public static void UpdateClass(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        DBservices db = new DBservices();
        db.UpdateClassEvent(eventId, from, to, recurring, recurrenceEndDate, courseId, location, duration);
    }

    // Updates a task-event's fields (used when the user drags a study block).
    public static void UpdateTaskEvent(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        DBservices db = new DBservices();
        db.UpdateTaskEvent(eventId, from, to, recurring, recurrenceEndDate, taskId, priority, status);
    }

    // Updates a work-event's fields.
    public static void UpdateWork(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime)
    {
        DBservices db = new DBservices();
        db.UpdateWorkEvent(eventId, from, to, recurring, recurrenceEndDate, workPlace, travelTime);
    }

    // Updates a personal-event's fields.
    public static void UpdatePersonal(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        DBservices db = new DBservices();
        db.UpdatePersonalEvent(eventId, from, to, recurring, recurrenceEndDate, type, description);
    }

    // Deletes the event row (cascades to subtype rows).
    public static void Delete(int eventId)
    {
        DBservices db = new DBservices();
        db.DeleteEvent(eventId);
    }

    // Returns the email of the event's owner (used to authorize updates/deletes).
    public static string? GetOwnerEmail(int eventId)
    {
        DBservices db = new DBservices();
        return db.GetEventOwnerEmail(eventId);
    }

    // Returns the event's subtype string ("class", "task", "work", "personal").
    public static string GetSubtype(int eventId)
    {
        DBservices db = new DBservices();
        return db.GetEventSubtype(eventId);
    }

    // Converts an event between Work and Personal subtypes by swapping subtype rows.
    public static void ChangeType(int eventId, string oldType, string newType, string? workPlace, int? travelTime, string? personalType, string? description)
    {
        DBservices db = new DBservices();
        db.ChangeEventType(eventId, oldType, newType, workPlace, travelTime, personalType, description);
    }

    // Counts task-event blocks that overlap the given time window (excluding one event).
    public static int CountConflictingTaskEvents(string email, DateTime from, DateTime to, int excludeEventId)
    {
        DBservices db = new DBservices();
        return db.CountConflictingTaskEvents(email, from, to, excludeEventId);
    }

    // Returns all events in the user's calendar that overlap the given time window.
    public static List<TypedEvent> GetConflicting(string email, DateTime from, DateTime to, int? excludeEventId)
    {
        DBservices db = new DBservices();
        return db.GetConflictingEvents(email, from, to, excludeEventId);
    }

    // Returns the (From, To) time range of the event, or null if not found.
    public static (DateTime From, DateTime To)? GetEventTimeRange(int eventId)
    {
        DBservices db = new DBservices();
        return db.GetEventTimeRange(eventId);
    }

    // Returns the partner's copy-task ID for a shared task, or null if not shared.
    public static int? GetSharedPartnerTaskId(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedPartnerTaskId(taskId);
    }

    // Mirrors a moved task-event onto the partner's calendar (returns the partner event ID).
    public static int? SyncSharedTaskEventMove(int movedEventId, int partnerTaskId,
        DateTime oldFrom, DateTime oldTo, DateTime newFrom, DateTime newTo)
    {
        DBservices db = new DBservices();
        return db.SyncSharedTaskEventMove(movedEventId, partnerTaskId, oldFrom, oldTo, newFrom, newTo);
    }

    // Marks a task as manually pinned so the auto-scheduler won't move it.
    public static void PinTask(int taskId)
    {
        DBservices db = new DBservices();
        db.PinTask(taskId);
    }
}

// ───── Event DTOs (from EventDtos.cs) ──────────────────────────────

// Wire-format event payload — superset of all subtypes' fields.
public class EventDto
{
    public int EventId { get; set; }
    public string EventType { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }

    // ClassEvent fields
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public string? Location { get; set; }
    public decimal? Duration { get; set; }

    // TaskEvent fields
    public int? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public string? Status { get; set; }
    public bool? IsManuallyPinned { get; set; }
    public bool IsShared { get; set; }
    public string? SharedStatus { get; set; }

    // WorkEvent fields
    public int? TravelTime { get; set; }
    public string? WorkPlace { get; set; }

    // PersonalEvent fields
    public string? Type { get; set; }
    public string? Description { get; set; }
}

// Request body for POST /api/events/class.
public class CreateClassEventDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int CourseId { get; set; }
    public string? Location { get; set; }
    public decimal? Duration { get; set; }
}

// Request body for POST /api/events/task.
public class CreateTaskEventDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int TaskId { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
}

// Request body for POST /api/events/work.
public class CreateWorkEventDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? TravelTime { get; set; }
    public string? WorkPlace { get; set; }
}

// Request body for POST /api/events/personal.
public class CreatePersonalEventDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}

// Request body for POST /api/events/check-conflicts.
public class CheckConflictDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? ExcludeEventId { get; set; }
}

// Request body for PUT /api/events/{id}/change-type (Work ↔ Personal swap).
public class ChangeEventTypeDto
{
    public string NewType { get; set; } = null!;
    // Work fields
    public string? WorkPlace { get; set; }
    public int? TravelTime { get; set; }
    // Personal fields
    public string? PersonalType { get; set; }
    public string? Description { get; set; }
}
