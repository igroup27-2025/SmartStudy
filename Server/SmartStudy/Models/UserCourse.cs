using SmartStudy.DAL;

namespace SmartStudy.Models;

// Junction record for the N:N enrollment of users into courses with study-partner data.
public class UserCourse
{
    public string Email { get; set; } = null!;
    public int CourseId { get; set; }
    public string? StudyPartnerEmail { get; set; }
    public bool SharedByDefault { get; set; } = false;
    public bool CourseShareApproved { get; set; } = false;

    // Navigation properties
    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;

    // ───── Static BLL methods ──────────────────────────────────

    // Returns the user's enrollment row for the given course, or null if not enrolled.
    public static UserCourse? Get(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.GetUserCourse(email, courseId);
    }

    // Marks the user's enrollment as approved for course-level task sharing.
    public static bool SetCourseShareApproved(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.SetCourseShareApproved(email, courseId);
    }

    // Lists pending shared-task memberships for this course, used after course-share approval.
    public static List<PendingMemberForCourseRow> GetPendingMembersForCourse(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.GetPendingMembersForCourse(email, courseId);
    }
}
