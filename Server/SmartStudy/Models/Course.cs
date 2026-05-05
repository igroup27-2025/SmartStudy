using SmartStudy.DAL;

namespace SmartStudy.Models;

// Course entity plus folded-in BLL methods for course CRUD.
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

    // ───── CoursesBLL methods folded in ──────────────────────────────────

    // Returns courses the user is enrolled in with task/exam counts and partner data.
    public static List<CourseWithEnrollment> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetCoursesByUser(email);
    }

    // Loads a single course by ID from the global courses table.
    public static Course? GetById(int courseId)
    {
        DBservices db = new DBservices();
        return db.GetCourseById(courseId);
    }

    // Returns true when the user is enrolled in the course (used to authorize updates).
    public static bool UserCourseExists(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.UserCourseExists(email, courseId);
    }

    // Returns the highest existing course ID, used to allocate the next manual course.
    public static int GetMaxCourseId()
    {
        DBservices db = new DBservices();
        return db.GetMaxCourseId();
    }

    // Inserts a new course row into the global courses table.
    public static void Create(int courseId, string courseName, decimal? weeklyHours, decimal? credits, string? semester, int? instructorId)
    {
        DBservices db = new DBservices();
        db.CreateCourse(courseId, courseName, weeklyHours, credits, semester, instructorId);
    }

    // Enrolls the user in the course by inserting into the UserCourses junction.
    public static void CreateUserCourse(string email, int courseId)
    {
        DBservices db = new DBservices();
        db.CreateUserCourse(email, courseId);
    }

    // Updates any subset of a course's fields (each parameter optional).
    public static void Update(int courseId, string? courseName = null, decimal? weeklyHours = null, decimal? credits = null,
        string? semester = null, int? instructorId = null, double? defaultTaskEstimatedHours = null,
        double? examPrepHoursPerDay = null, int? examPrepDays = null)
    {
        DBservices db = new DBservices();
        db.UpdateCourse(courseId, courseName, weeklyHours, credits, semester, instructorId,
            defaultTaskEstimatedHours, examPrepHoursPerDay, examPrepDays);
    }

    // Sets whether the user's tasks for this course should be auto-shared with their study partner.
    public static void UpdateSharedByDefault(string email, int courseId, bool sharedByDefault)
    {
        DBservices db = new DBservices();
        db.UpdateSharedByDefault(email, courseId, sharedByDefault);
    }

    // Sets or clears the study partner assigned to the user's enrollment.
    public static void UpdateStudyPartner(string email, int courseId, string? partnerEmail)
    {
        DBservices db = new DBservices();
        db.UpdateStudyPartner(email, courseId, partnerEmail);
    }

    // Removes the user's enrollment in the course (does not delete the global course).
    public static void DeleteUserCourse(string email, int courseId)
    {
        DBservices db = new DBservices();
        db.DeleteUserCourse(email, courseId);
    }
}

// ───── Course DTOs (from CourseDtos.cs) ────────────────────────────

// Wire-format response shape for the Courses API.
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
    public bool SharedByDefault { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}

// Request body for PUT /api/courses/{id}/partner.
public class SetStudyPartnerDto
{
    public string? Email { get; set; }
}

// Request body for POST /api/courses.
public class CreateCourseDto
{
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
}

// Request body for PUT /api/courses/{id} — all fields optional.
public class UpdateCourseDto
{
    public string? CourseName { get; set; }
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
    public bool? SharedByDefault { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}
