namespace SmartStudy.DTOs;

public class ExamDto
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
    public int DaysUntil { get; set; }
}

public class CreateExamDto
{
    public int CourseId { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = null!;
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
}

public class UpdateExamDto
{
    public int? CourseId { get; set; }
    public DateTime? Date { get; set; }
    public string? Time { get; set; }
    public string? Session { get; set; }
    public int? Duration { get; set; }
}
