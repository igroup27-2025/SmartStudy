namespace SmartStudy.Models;

public class Event
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
