namespace SmartStudy.DTOs;

public class RuppinetConnectDto
{
    public string RuppinetId { get; set; } = null!;
    public string RuppinetPassword { get; set; } = null!;
}

public class RuppinetSyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
    public int CoursesCreated { get; set; }
    public int CoursesUpdated { get; set; }
    public int ExamsCreated { get; set; }
    public int ExamsUpdated { get; set; }
    public int ClassEventsCreated { get; set; }
    public int ClassEventsSkipped { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class RuppinetStatusDto
{
    public bool IsConnected { get; set; }
    public string? RuppinetId { get; set; }
    public DateTime? LastSync { get; set; }
}
