namespace SmartStudy.Models;

public class Friendship
{
    public int FriendshipId { get; set; }
    public string Email1 { get; set; } = null!; // Alphabetically smaller
    public string Email2 { get; set; } = null!; // Alphabetically larger
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User User1 { get; set; } = null!;
    public User User2 { get; set; } = null!;
}
