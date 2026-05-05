using SmartStudy.DAL;

namespace SmartStudy.Models;

// 1:1 extension of StudentTask that records peer collaboration (creator + members + status).
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

    // Returns flat join rows for every shared task the user is a member of.
    public static List<SharedTaskFullRow> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetSharedTasksByUser(email);
    }

    // Returns flat join rows for one shared task (one row per member).
    public static List<SharedTaskFullRow> GetByTaskId(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedTaskByTaskId(taskId);
    }

    // Returns true if the task is already shared.
    public static bool Exists(int taskId)
    {
        DBservices db = new DBservices();
        return db.SharedTaskExists(taskId);
    }

    // Creates the SharedTask row that turns a regular task into a peer-shared one.
    public static void Create(int taskId, string createdByEmail, string sharedStatus = "Pending")
    {
        DBservices db = new DBservices();
        db.CreateSharedTask(taskId, createdByEmail, sharedStatus);
    }

    // Updates the shared task's status (Pending, Confirmed, Cancelled).
    public static void UpdateStatus(int taskId, string status)
    {
        DBservices db = new DBservices();
        db.UpdateSharedTaskStatus(taskId, status);
    }

    // Inserts a member row into a shared task with their initial response status.
    public static void CreateMember(int taskId, string email, string responseStatus = "Pending", DateTime? respondedAt = null)
    {
        DBservices db = new DBservices();
        db.CreateSharedTaskMember(taskId, email, responseStatus, respondedAt);
    }

    // Updates a single member's response status (Accepted/Declined); returns true if a row changed.
    public static bool UpdateMemberStatus(int taskId, string email, string responseStatus)
    {
        DBservices db = new DBservices();
        return db.UpdateSharedTaskMemberStatus(taskId, email, responseStatus);
    }

    // Returns true when every member's status is Accepted (used to confirm a shared task).
    public static bool AllMembersAccepted(int taskId)
    {
        DBservices db = new DBservices();
        return db.AllSharedTaskMembersAccepted(taskId);
    }

    // Returns the list of every member's email for a shared task.
    public static List<string> GetMemberEmails(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedTaskMemberEmails(taskId);
    }

    // Removes the partner-side task copies after a shared task is cancelled or declined.
    public static int CleanupPartnerCopies(int taskId)
    {
        DBservices db = new DBservices();
        return db.CleanupSharedTaskPartnerCopies(taskId);
    }
}

// ───── DTOs (from SharedTaskDtos.cs) ───────────────────────────────

// Wire-format response shape for one shared task with its members.
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

// Wire-format member sub-row inside a SharedTaskDto.
public class SharedTaskMemberDto
{
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
}

// Request body for POST /api/shared-tasks (invite a friend to share a task).
public class CreateSharedTaskDto
{
    public int TaskId { get; set; }
    public string PartnerEmail { get; set; } = null!;
}

// Request body for POST /api/shared-tasks/{taskId}/respond.
public class RespondSharedTaskDto
{
    public bool Accept { get; set; }
}
