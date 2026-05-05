using SmartStudy.DAL;

namespace SmartStudy.Models;

// Domain entity for an exam date tied to a course, with session (A/B/C) and duration.
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

    // Returns all exams for the user's enrolled courses, with course names.
    public static List<ExamWithCourse> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetExamsByUser(email);
    }

    // Returns one exam by ID if it belongs to a course the user is enrolled in.
    public static ExamWithCourse? GetById(int examId, string email)
    {
        DBservices db = new DBservices();
        return db.GetExamById(examId, email);
    }

    // Inserts a new exam row and returns its generated ID.
    public static int Create(int courseId, DateTime date, TimeSpan time, string session, int? duration, bool isTakingExam)
    {
        DBservices db = new DBservices();
        return db.CreateExam(courseId, date, time, session, duration, isTakingExam);
    }

    // Updates any subset of an exam's fields (only non-null parameters take effect).
    public static void Update(int examId, int? courseId = null, DateTime? date = null, TimeSpan? time = null, string? session = null, int? duration = null)
    {
        DBservices db = new DBservices();
        db.UpdateExam(examId, courseId, date, time, session, duration);
    }

    // Flips the IsTakingExam flag for the exam.
    public static void ToggleTaking(int examId)
    {
        DBservices db = new DBservices();
        db.ToggleExamTaking(examId);
    }

    // Deletes the exam row by ID.
    public static void Delete(int examId)
    {
        DBservices db = new DBservices();
        db.DeleteExam(examId);
    }

    // Removes the auto-generated exam-prep tasks when the user opts out of an exam.
    public static void DeleteStudyTasksForExam(string email, int courseId, DateTime examDate)
    {
        DBservices db = new DBservices();
        db.DeleteStudyTasksForExam(email, courseId, examDate);
    }
}

// ───── DTOs (from ExamDtos.cs) ─────────────────────────────────────

// Wire-format response shape returned by the Exams API.
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

// Request body for POST /api/exams.
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

// Request body for PUT /api/exams/{id} — all fields optional.
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
