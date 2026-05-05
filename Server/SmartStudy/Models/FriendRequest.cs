namespace SmartStudy.Models;

// Pending/Accepted/Rejected friend invite from one user to another.
public class FriendRequest
{
    public int RequestId { get; set; }
    public string RequesterEmail { get; set; } = null!;
    public string AddresseeEmail { get; set; } = null!;
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    // Navigation properties
    public User Requester { get; set; } = null!;
    public User Addressee { get; set; } = null!;
}
