using SmartStudy.DAL;

namespace SmartStudy.Models;

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

    public static List<TypedEvent> GetAllTypedEventsInRange(string email, DateTime? from, DateTime? to)
    {
        DBservices db = new DBservices();
        return db.GetAllTypedEventsInRange(email, from, to);
    }

    public static int CreateClass(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        DBservices db = new DBservices();
        return db.CreateClassEvent(email, from, to, recurring, recurrenceEndDate, courseId, location, duration);
    }

    public static int CreateTaskEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        DBservices db = new DBservices();
        return db.CreateTaskEvent(email, from, to, recurring, recurrenceEndDate, taskId, priority, status);
    }

    public static int CreateWork(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime = null)
    {
        DBservices db = new DBservices();
        return db.CreateWorkEvent(email, from, to, recurring, recurrenceEndDate, workPlace, travelTime);
    }

    public static int CreatePersonal(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        DBservices db = new DBservices();
        return db.CreatePersonalEvent(email, from, to, recurring, recurrenceEndDate, type, description);
    }

    public static void UpdateClass(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        DBservices db = new DBservices();
        db.UpdateClassEvent(eventId, from, to, recurring, recurrenceEndDate, courseId, location, duration);
    }

    public static void UpdateTaskEvent(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        DBservices db = new DBservices();
        db.UpdateTaskEvent(eventId, from, to, recurring, recurrenceEndDate, taskId, priority, status);
    }

    public static void UpdateWork(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime)
    {
        DBservices db = new DBservices();
        db.UpdateWorkEvent(eventId, from, to, recurring, recurrenceEndDate, workPlace, travelTime);
    }

    public static void UpdatePersonal(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        DBservices db = new DBservices();
        db.UpdatePersonalEvent(eventId, from, to, recurring, recurrenceEndDate, type, description);
    }

    public static void Delete(int eventId)
    {
        DBservices db = new DBservices();
        db.DeleteEvent(eventId);
    }

    public static string? GetOwnerEmail(int eventId)
    {
        DBservices db = new DBservices();
        return db.GetEventOwnerEmail(eventId);
    }

    public static string GetSubtype(int eventId)
    {
        DBservices db = new DBservices();
        return db.GetEventSubtype(eventId);
    }

    public static void ChangeType(int eventId, string oldType, string newType, string? workPlace, int? travelTime, string? personalType, string? description)
    {
        DBservices db = new DBservices();
        db.ChangeEventType(eventId, oldType, newType, workPlace, travelTime, personalType, description);
    }

    public static int CountConflictingTaskEvents(string email, DateTime from, DateTime to, int excludeEventId)
    {
        DBservices db = new DBservices();
        return db.CountConflictingTaskEvents(email, from, to, excludeEventId);
    }

    public static List<TypedEvent> GetConflicting(string email, DateTime from, DateTime to, int? excludeEventId)
    {
        DBservices db = new DBservices();
        return db.GetConflictingEvents(email, from, to, excludeEventId);
    }

    public static (DateTime From, DateTime To)? GetEventTimeRange(int eventId)
    {
        DBservices db = new DBservices();
        return db.GetEventTimeRange(eventId);
    }

    public static int? GetSharedPartnerTaskId(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedPartnerTaskId(taskId);
    }

    public static int? SyncSharedTaskEventMove(int movedEventId, int partnerTaskId,
        DateTime oldFrom, DateTime oldTo, DateTime newFrom, DateTime newTo)
    {
        DBservices db = new DBservices();
        return db.SyncSharedTaskEventMove(movedEventId, partnerTaskId, oldFrom, oldTo, newFrom, newTo);
    }

    public static void PinTask(int taskId)
    {
        DBservices db = new DBservices();
        db.PinTask(taskId);
    }
}

// ───── Event DTOs (from EventDtos.cs) ──────────────────────────────

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

public class CreateWorkEventDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? TravelTime { get; set; }
    public string? WorkPlace { get; set; }
}

public class CreatePersonalEventDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}

public class CheckConflictDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? ExcludeEventId { get; set; }
}

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
