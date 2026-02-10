namespace SmartStudy.DTOs;

public class SchedulingResultDto
{
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
    public List<ScheduledTaskDto> ScheduledTasks { get; set; } = new();
    public List<UnscheduledTaskDto> UnscheduledTasks { get; set; } = new();
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
}

public class SchedulingStatusDto
{
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
}
