namespace SmartStudy.Models;

// Calendar event subtype representing a scheduled study block for a task.
public class TaskEvent : Event
{
    public int TaskId { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public string? Status { get; set; }

    // Navigation property
    public StudentTask StudentTask { get; set; } = null!;
}
