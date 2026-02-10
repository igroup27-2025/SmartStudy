namespace SmartStudy.Models;

public class StudentTask
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public string Email { get; set; } = null!;

    // Navigation properties
    public Course Course { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<TaskEvent> TaskEvents { get; set; } = new List<TaskEvent>();
    public SharedTask? SharedTask { get; set; }
}
