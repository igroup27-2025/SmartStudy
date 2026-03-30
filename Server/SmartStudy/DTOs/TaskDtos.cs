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
    public decimal? ActualHours { get; set; }

    // Sub-task fields
    public int? ParentTaskId { get; set; }
    public List<TaskDto>? SubTasks { get; set; }
    public int SubTaskCount { get; set; }
    public int CompletedSubTaskCount { get; set; }
    public double SubTaskProgress { get; set; }

    // Shared task fields
    public bool IsShared { get; set; }
    public string? SharedStatus { get; set; }
    public string? SharedWithName { get; set; }

    // Scheduling fields
    public DateTime? ScheduledDate { get; set; }
    public string SchedulingStatus { get; set; } = "Unscheduled"; // "Scheduled" | "Unscheduled" | "Partial"
    public List<TaskSlotDto>? ScheduledSlots { get; set; }
    public bool AllowSplitting { get; set; }
    public bool IsManuallyPinned { get; set; }
    public bool IsManualPriority { get; set; }
}

public class TaskSlotDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class CreateTaskDto
{
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public int? ParentTaskId { get; set; }
    public bool AllowSplitting { get; set; } = false;
    public string? Priority { get; set; }
}

public class CompleteTaskDto
{
    public decimal? ActualHours { get; set; }
}

public class SplitTaskDto
{
    public List<SubTaskDefinition> SubTasks { get; set; } = new();
}

public class SubTaskDefinition
{
    public string Title { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
}

public class UpdateTaskDto
{
    public int? CourseId { get; set; }
    public string? Title { get; set; }
    public string? Type { get; set; }
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool? IsCompleted { get; set; }
    public bool? AllowSplitting { get; set; }
    public bool? IsManuallyPinned { get; set; }
    public string? Priority { get; set; }
}
