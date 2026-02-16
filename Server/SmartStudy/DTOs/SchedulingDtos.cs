namespace SmartStudy.DTOs;

public class SchedulingResultDto
{
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
    public List<ScheduledTaskDto> ScheduledTasks { get; set; } = new();
    public List<UnscheduledTaskDto> UnscheduledTasks { get; set; } = new();
    public List<RelocationSuggestionDto> RelocationSuggestions { get; set; } = new();
}

public class ScheduledTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public List<ScheduledSlotDto> Slots { get; set; } = new();
}

public class UnscheduledTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string Reason { get; set; } = null!;
}

public class ScheduledSlotDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class DailyWorkloadDto
{
    public DateTime Date { get; set; }
    public double ScheduledHours { get; set; }
    public double AvailableHours { get; set; }
    public bool IsOverloaded { get; set; }
    public double StudyHours { get; set; }
    public double WorkHours { get; set; }
    public double ClassHours { get; set; }
    public double PersonalHours { get; set; }
    public double TotalHours { get; set; }
}

public class RelocationSuggestionDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public DateTime CurrentFrom { get; set; }
    public DateTime CurrentTo { get; set; }
    public string BlockedTaskTitle { get; set; } = null!;
    public string Message { get; set; } = null!;
}

public class SchedulingStatusDto
{
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
}
