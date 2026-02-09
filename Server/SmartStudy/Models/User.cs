namespace SmartStudy.Models;

public class User
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Password { get; set; } = null!;

    // Navigation properties
    public NotificationSettings? NotificationSettings { get; set; }
    public ICollection<UserCourse> UserCourses { get; set; } = new List<UserCourse>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<StudentTask> Tasks { get; set; } = new List<StudentTask>();
}
