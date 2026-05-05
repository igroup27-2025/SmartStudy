namespace SmartStudy.Models;

// Calendar event subtype for work shifts including travel time and workplace.
public class WorkEvent : Event
{
    public int? TravelTime { get; set; }
    public string? WorkPlace { get; set; }
}
