using SmartStudy.DAL;

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

    // ───── ExamsBLL methods folded in ───────────────────────────────────

    public static List<ExamWithCourse> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetExamsByUser(email);
    }

    public static ExamWithCourse? GetById(int examId, string email)
    {
        DBservices db = new DBservices();
        return db.GetExamById(examId, email);
    }

    public static int Create(int courseId, DateTime date, TimeSpan time, string session, int? duration, bool isTakingExam)
    {
        DBservices db = new DBservices();
        return db.CreateExam(courseId, date, time, session, duration, isTakingExam);
    }

    public static void Update(int examId, int? courseId = null, DateTime? date = null, TimeSpan? time = null, string? session = null, int? duration = null)
    {
        DBservices db = new DBservices();
        db.UpdateExam(examId, courseId, date, time, session, duration);
    }

    public static void ToggleTaking(int examId)
    {
        DBservices db = new DBservices();
        db.ToggleExamTaking(examId);
    }

    public static void Delete(int examId)
    {
        DBservices db = new DBservices();
        db.DeleteExam(examId);
    }

    public static void DeleteStudyTasksForExam(string email, int courseId, DateTime examDate)
    {
        DBservices db = new DBservices();
        db.DeleteStudyTasksForExam(email, courseId, examDate);
    }
}

// ───── DTOs (from ExamDtos.cs) ─────────────────────────────────────

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
    public bool IsTakingExam { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}

public class CreateExamDto
{
    public int CourseId { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = null!;
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}

public class UpdateExamDto
{
    public int? CourseId { get; set; }
    public DateTime? Date { get; set; }
    public string? Time { get; set; }
    public string? Session { get; set; }
    public int? Duration { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}
