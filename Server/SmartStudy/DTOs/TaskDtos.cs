namespace SmartStudy.DTOs;

public class TaskDto
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
}

public class CreateTaskDto
{
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }
}

public class UpdateTaskDto
{
    public int? CourseId { get; set; }
    public string? Title { get; set; }
    public string? Type { get; set; }
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }
    public bool? IsCompleted { get; set; }
}
