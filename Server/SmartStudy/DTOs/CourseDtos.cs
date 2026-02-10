namespace SmartStudy.DTOs;

public class CourseDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public int TaskCount { get; set; }
    public int ExamCount { get; set; }
    public string? StudyPartnerEmail { get; set; }
    public string? StudyPartnerName { get; set; }
}

public class SetStudyPartnerDto
{
    public string? Email { get; set; }
}

public class CreateCourseDto
{
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
}

public class UpdateCourseDto
{
    public string? CourseName { get; set; }
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
}
