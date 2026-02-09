namespace SmartStudy.Models;

public class UserCourse
{
    public string Email { get; set; } = null!;
    public int CourseId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
