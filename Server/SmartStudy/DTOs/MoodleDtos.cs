namespace SmartStudy.DTOs;

public class MoodleSyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
    public int TasksCreated { get; set; }
    public int TasksUpdated { get; set; }
    public int TasksSkipped { get; set; }
    public int CoursesMatched { get; set; }
    public int CoursesCreated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class MoodleStatusDto
{
    public bool IsAvailable { get; set; }
    public DateTime? LastSync { get; set; }
}
