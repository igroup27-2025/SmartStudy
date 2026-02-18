namespace SmartStudy.Models;

public class Exam
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
    public bool IsTakingExam { get; set; } = true;

    // Navigation property
    public Course Course { get; set; } = null!;
}
