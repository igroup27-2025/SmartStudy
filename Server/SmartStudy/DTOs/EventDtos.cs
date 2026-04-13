namespace SmartStudy.DTOs;

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
