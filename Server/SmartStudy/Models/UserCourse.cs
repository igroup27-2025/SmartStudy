using SmartStudy.DAL;

namespace SmartStudy.Models;

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

    public static UserCourse? Get(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.GetUserCourse(email, courseId);
    }

    public static bool SetCourseShareApproved(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.SetCourseShareApproved(email, courseId);
    }

    public static List<PendingMemberForCourseRow> GetPendingMembersForCourse(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.GetPendingMembersForCourse(email, courseId);
    }
}
