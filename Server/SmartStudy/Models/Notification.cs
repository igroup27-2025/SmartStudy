namespace SmartStudy.Models;

public class Notification
{
    public int NotificationId { get; set; }
    public string Email { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
