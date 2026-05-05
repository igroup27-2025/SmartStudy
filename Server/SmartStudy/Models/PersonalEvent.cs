namespace SmartStudy.Models;

// Calendar event subtype for personal blocks (sleep, meals, gym, etc).
public class PersonalEvent : Event
{
    public string? Type { get; set; }
    public string? Description { get; set; }
}
