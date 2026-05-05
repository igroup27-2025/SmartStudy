namespace SmartStudy.Models;

// Junction row linking a SharedTask to a participating user with their response status.
public class SharedTaskMember
{
    public int TaskId { get; set; }   // PK part 1, FK → SharedTask
    public string Email { get; set; } = null!; // PK part 2, FK → User
    public string ResponseStatus { get; set; } = "Pending"; // Pending, Accepted, Declined
    public DateTime? RespondedAt { get; set; }
    public int? CopyTaskId { get; set; }   // partner's copy of the shared task

    // Navigation properties
    public SharedTask SharedTask { get; set; } = null!;
    public User User { get; set; } = null!;
}
