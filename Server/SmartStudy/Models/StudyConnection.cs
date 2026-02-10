namespace SmartStudy.Models;

public class StudyConnection
{
    public int ConnectionId { get; set; }
    public string RequesterEmail { get; set; } = null!;
    public string ReceiverEmail { get; set; } = null!;
    public string Status { get; set; } = "Pending"; // Pending, Accepted
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // Navigation properties
    public User Requester { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}
