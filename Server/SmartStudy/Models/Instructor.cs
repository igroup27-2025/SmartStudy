namespace SmartStudy.Models;

public class Instructor
{
    public int InstructorId { get; set; }
    public string InstructorName { get; set; } = null!;

    // Navigation property
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
