namespace SmartStudy.Models;

public class SharedTask
{
    public int TaskId { get; set; } // PK + FK → StudentTask (1:1)
    public string CreatedByEmail { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string SharedStatus { get; set; } = "Draft"; // Draft, Pending, Confirmed, Cancelled

    // Navigation properties
    public StudentTask Task { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<SharedTaskMember> Members { get; set; } = new List<SharedTaskMember>();
}
