namespace SmartStudy.Models;

public class User
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string? AuthProvider { get; set; }

    public bool OnboardingCompleted { get; set; } = false;

    // Google Calendar integration
    public string? GoogleCalendarAccessToken { get; set; }
    public string? GoogleCalendarRefreshToken { get; set; }
    public DateTime? LastCalendarSync { get; set; }

    // Composio integration
    public string? ComposioConnectedAccountId { get; set; }

    // Ruppinet integration
    public string? RuppinetId { get; set; }
    public string? RuppinetPassword { get; set; }
    public DateTime? LastRuppinetSync { get; set; }

    // Navigation properties
    public NotificationSettings? NotificationSettings { get; set; }
    public SchedulingPreferences? SchedulingPreferences { get; set; }
    public ICollection<UserCourse> UserCourses { get; set; } = new List<UserCourse>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<StudentTask> Tasks { get; set; } = new List<StudentTask>();
    public ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
    public ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
    public ICollection<Friendship> FriendshipsAsUser1 { get; set; } = new List<Friendship>();
    public ICollection<Friendship> FriendshipsAsUser2 { get; set; } = new List<Friendship>();
    public ICollection<SharedTaskMember> SharedTaskMemberships { get; set; } = new List<SharedTaskMember>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
