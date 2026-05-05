using SmartStudy.DAL;

namespace SmartStudy.Models;

public class SharedTask
{
    public int TaskId { get; set; } // PK + FK → StudentTask (1:1)
    public string CreatedByEmail { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string SharedStatus { get; set; } = "Draft"; // Draft, Pending, Confirmed, Cancelled

    // Navigation properties
    public StudentTask Task { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<SharedTaskMember> Members { get; set; } = new List<SharedTaskMember>();

    // ───── SharedTaskBLL methods folded in ──────────────────────────────

    public static List<SharedTaskFullRow> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetSharedTasksByUser(email);
    }

    public static List<SharedTaskFullRow> GetByTaskId(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedTaskByTaskId(taskId);
    }

    public static bool Exists(int taskId)
    {
        DBservices db = new DBservices();
        return db.SharedTaskExists(taskId);
    }

    public static void Create(int taskId, string createdByEmail, string sharedStatus = "Pending")
    {
        DBservices db = new DBservices();
        db.CreateSharedTask(taskId, createdByEmail, sharedStatus);
    }

    public static void UpdateStatus(int taskId, string status)
    {
        DBservices db = new DBservices();
        db.UpdateSharedTaskStatus(taskId, status);
    }

    public static void CreateMember(int taskId, string email, string responseStatus = "Pending", DateTime? respondedAt = null)
    {
        DBservices db = new DBservices();
        db.CreateSharedTaskMember(taskId, email, responseStatus, respondedAt);
    }

    public static bool UpdateMemberStatus(int taskId, string email, string responseStatus)
    {
        DBservices db = new DBservices();
        return db.UpdateSharedTaskMemberStatus(taskId, email, responseStatus);
    }

    public static bool AllMembersAccepted(int taskId)
    {
        DBservices db = new DBservices();
        return db.AllSharedTaskMembersAccepted(taskId);
    }

    public static List<string> GetMemberEmails(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedTaskMemberEmails(taskId);
    }

    public static int CleanupPartnerCopies(int taskId)
    {
        DBservices db = new DBservices();
        return db.CleanupSharedTaskPartnerCopies(taskId);
    }
}

// ───── DTOs (from SharedTaskDtos.cs) ───────────────────────────────

public class SharedTaskDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = null!;
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public string CreatedByEmail { get; set; } = null!;
    public string CreatedByName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string SharedStatus { get; set; } = null!;
    public List<SharedTaskMemberDto> Members { get; set; } = new();
}

public class SharedTaskMemberDto
{
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
}

public class CreateSharedTaskDto
{
    public int TaskId { get; set; }
    public string PartnerEmail { get; set; } = null!;
}

public class RespondSharedTaskDto
{
    public bool Accept { get; set; }
}
