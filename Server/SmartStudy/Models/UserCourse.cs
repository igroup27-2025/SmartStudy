namespace SmartStudy.Models;

public class UserCourse
{
    public string Email { get; set; } = null!;
    public int CourseId { get; set; }
    public string? StudyPartnerEmail { get; set; }
    public bool SharedByDefault { get; set; } = false;

    // Navigation properties
    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
