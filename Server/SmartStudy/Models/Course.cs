namespace SmartStudy.Models;

public class Course
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }

    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }

    // Navigation properties
    public Instructor? Instructor { get; set; }
    public ICollection<UserCourse> UserCourses { get; set; } = new List<UserCourse>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public ICollection<StudentTask> Tasks { get; set; } = new List<StudentTask>();
    public ICollection<ClassEvent> ClassEvents { get; set; } = new List<ClassEvent>();
}
