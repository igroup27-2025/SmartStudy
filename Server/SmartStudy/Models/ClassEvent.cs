namespace SmartStudy.Models;

// Calendar event subtype for a scheduled class meeting tied to a course.
public class ClassEvent : Event
{
    public int CourseId { get; set; }
    public string? Location { get; set; }
    public decimal? Duration { get; set; }

    // Navigation property
    public Course Course { get; set; } = null!;
}
