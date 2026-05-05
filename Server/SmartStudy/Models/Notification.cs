using SmartStudy.DAL;

namespace SmartStudy.Models;

// In-app notification entity plus the static helpers that generate per-user notifications.
public class Notification
{
    public int NotificationId { get; set; }
    public string Email { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    // Navigation property
    public User User { get; set; } = null!;

    // ───── NotificationsBLL methods folded in ──────────────────────────

    // Returns the user's notifications and the unread count.
    public static (List<NotificationRow> Notifications, int UnreadCount) GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetNotificationsByUser(email);
    }

    // Returns just the unread notification count for badge polling.
    public static int GetUnreadCount(string email)
    {
        DBservices db = new DBservices();
        return db.GetUnreadNotificationCount(email);
    }

    // Marks the specified notifications as read for the user.
    public static void MarkRead(string email, List<int> notificationIds)
    {
        DBservices db = new DBservices();
        db.MarkNotificationsRead(email, notificationIds);
    }

    // Marks every one of the user's notifications as read.
    public static void MarkAllRead(string email)
    {
        DBservices db = new DBservices();
        db.MarkAllNotificationsRead(email);
    }

    // Inserts a generic notification row; returns its ID.
    public static int Create(string email, string type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null)
    {
        DBservices db = new DBservices();
        return db.CreateNotification(email, type, title, message, relatedEntityId, relatedEntityType);
    }

    // ───── NotificationService methods folded in ───────────────────────

    // Creates "due in 24h" alerts for each urgent task, respecting user prefs and quiet hours.
    public static void GenerateDeadline(string email)
    {
        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(email);
        if (settings == null || !settings.NotifyBeforeTask) return;
        if (IsInQuietHours(settings)) return;

        var urgentTasks = db.GetUpcomingDeadlineTasks(email);

        foreach (var task in urgentTasks)
        {
            if (db.IsNotificationDuplicate(email, "deadline", task.TaskId, "Task"))
                continue;

            db.CreateNotification(
                email,
                "deadline",
                "Task Due Soon",
                $"\"{task.Title}\" ({task.CourseName}) is due within 24 hours!",
                task.TaskId,
                "Task");
        }
    }

    // Creates a "high stress" alert when the score crosses 70%.
    // Suppresses new alerts while a prior overload notification is still unread, so they don't pile up.
    public static void GenerateOverload(string email, double stressScore)
    {
        if (stressScore <= 70) return;

        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(email);
        if (settings == null) return;
        if (IsInQuietHours(settings)) return;

        if (db.HasUnreadNotificationOfType(email, "overload"))
            return;

        db.CreateNotification(
            email,
            "overload",
            "High Stress Alert",
            $"Your stress level is at {stressScore:F0}%. Consider rescheduling or asking for help.",
            null,
            "Stress");
    }

    // Notifies the recipient that a friend invited them to share a task.
    public static void CreateSharedTaskInvite(string recipientEmail, string senderName, int taskId, string taskTitle)
    {
        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(recipientEmail);
        if (settings == null) return;
        if (IsInQuietHours(settings)) return;

        db.CreateNotification(
            recipientEmail,
            "shared_task_invite",
            "Shared Task Invitation",
            $"{senderName} invited you to collaborate on \"{taskTitle}\".",
            taskId,
            "SharedTask");
    }

    // Notifies the task creator that a partner accepted or declined the shared task.
    public static void CreateSharedTaskResponse(string recipientEmail, string responderName, int taskId, string taskTitle, bool accepted)
    {
        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(recipientEmail);
        if (settings == null) return;
        if (IsInQuietHours(settings)) return;

        db.CreateNotification(
            recipientEmail,
            "shared_task_response",
            accepted ? "Task Accepted" : "Task Declined",
            $"{responderName} {(accepted ? "accepted" : "declined")} the shared task \"{taskTitle}\".",
            taskId,
            "SharedTask");
    }

    // Creates a morning-summary notification listing today's upcoming tasks.
    public static void GenerateDailySummary(string email)
    {
        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(email);
        if (settings == null || !settings.DailyMorningSummary) return;
        if (IsInQuietHours(settings)) return;

        if (db.IsNotificationDuplicate(email, "daily_summary", null, null))
            return;

        var todayTasks = db.GetDailySummaryData(email);
        var taskCount = todayTasks.Count;

        if (taskCount == 0)
        {
            db.CreateNotification(
                email,
                "daily_summary",
                "Good Morning!",
                "No urgent tasks today. Keep up the great work!");
        }
        else
        {
            var taskNames = string.Join(", ", todayTasks.Select(t => t.Title).Take(3));
            var more = taskCount > 3 ? $" and {taskCount - 3} more" : "";
            db.CreateNotification(
                email,
                "daily_summary",
                "Daily Summary",
                $"You have {taskCount} upcoming task(s): {taskNames}{more}.");
        }
    }

    // Creates a weekly plan reminder summarizing this week's tasks and exams.
    public static void GenerateWeeklyPlanReminder(string email)
    {
        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(email);
        if (settings == null || !settings.WeeklyPlanReminder) return;
        if (IsInQuietHours(settings)) return;

        if (db.HasRecentWeeklyReminder(email))
            return;

        var (weekTasks, weekExams) = db.GetWeeklyPlanData(email);

        db.CreateNotification(
            email,
            "weekly_reminder",
            "Weekly Plan Reminder",
            $"This week: {weekTasks} task(s) due{(weekExams > 0 ? $" and {weekExams} exam(s)" : "")}. Review your schedule and plan ahead!");
    }

    // Notifies a partner when no overlapping free slot was found for a shared task.
    public static void CreateNoCommonTime(string recipientEmail, int taskId, string taskTitle)
    {
        var db = new DBservices();
        var settings = db.GetNotifSettingsByEmail(recipientEmail);
        if (settings == null) return;
        if (IsInQuietHours(settings)) return;

        db.CreateNotification(
            recipientEmail,
            "shared_task_no_time",
            "No Common Time Found",
            $"Could not find a common study time for \"{taskTitle}\". The task has been scheduled independently for each of you.",
            taskId,
            "SharedTask");
    }

    // Returns true if the current time falls inside the user's configured quiet-hours window.
    private static bool IsInQuietHours(NotificationSettings? settings)
    {
        if (settings == null) return false;
        if (!settings.QuietHoursStart.HasValue || !settings.QuietHoursEnd.HasValue) return false;

        var now = DateTime.Now.TimeOfDay;
        var start = settings.QuietHoursStart.Value;
        var end = settings.QuietHoursEnd.Value;

        if (start > end)
            return now >= start || now <= end;

        return now >= start && now <= end;
    }
}

// ───── DTOs (from NotificationDtos.cs) ─────────────────────────────

// Wire-format payload for a single notification.
public class NotificationDto
{
    public int NotificationId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

// Wrapper returned by GET /api/notifications: list plus unread count.
public class NotificationListDto
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
}

// Request body for POST /api/notifications/mark-read.
public class MarkReadDto
{
    public List<int> NotificationIds { get; set; } = new();
}
