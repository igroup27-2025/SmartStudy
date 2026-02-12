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

    // Scheduling preferences (collected during onboarding, editable in Settings)
    public double MaxDailyStudyHours { get; set; } = 6.0;
    public int MaxContinuousMinutes { get; set; } = 90;
    public int DayStartHour { get; set; } = 8;
    public int DayEndHour { get; set; } = 22;
    public double SleepHoursPerDay { get; set; } = 8.0;
    public TimeSpan? LunchBreakStart { get; set; }
    public TimeSpan? LunchBreakEnd { get; set; }
    public bool OnboardingCompleted { get; set; } = false;

    // Navigation properties
    public NotificationSettings? NotificationSettings { get; set; }
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
