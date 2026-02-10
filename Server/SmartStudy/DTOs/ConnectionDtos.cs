namespace SmartStudy.DTOs;

public class ConnectionDto
{
    public int ConnectionId { get; set; }
    public string FriendEmail { get; set; } = null!;
    public string FriendName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? ConnectedDate { get; set; }
}

public class InviteConnectionDto
{
    public string Email { get; set; } = null!;
}

public class SafeZoneDto
{
    public string Date { get; set; } = null!;
    public string Day { get; set; } = null!;
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
    public double MyStress { get; set; }
    public double FriendStress { get; set; }
}
