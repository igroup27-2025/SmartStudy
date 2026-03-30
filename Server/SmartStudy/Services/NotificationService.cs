using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class NotificationService
{
    private readonly SmartStudyDbContext _db;

    public NotificationService(SmartStudyDbContext db)
    {
        _db = db;
    }

    public async Task GenerateDeadlineNotificationsAsync(string email)
    {
        // Check if deadline notifications are enabled
        var settings = await _db.NotificationSettings.FindAsync(email);
        if (settings != null && !settings.NotifyBeforeTask) return;

        // Check quiet hours
        if (IsInQuietHours(settings)) return;

        var now = DateTime.Now;
        var in24h = now.AddHours(24);

        var urgentTasks = await _db.Tasks
            .Include(t => t.Course)
            .Where(t => t.Email == email && !t.IsCompleted
                && t.DueDate.HasValue && t.DueDate > now && t.DueDate <= in24h)
            .ToListAsync();

        foreach (var task in urgentTasks)
        {
            if (await IsDuplicate(email, "deadline", task.TaskId, "Task"))
                continue;

            _db.Notifications.Add(new Notification
            {
                Email = email,
                Type = "deadline",
                Title = "Task Due Soon",
                Message = $"\"{task.Title}\" ({task.Course?.CourseName}) is due within 24 hours!",
                CreatedAt = now,
                RelatedEntityId = task.TaskId,
                RelatedEntityType = "Task"
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task GenerateOverloadNotificationAsync(string email, double stressScore)
    {
        if (stressScore <= 70) return;

        // Check quiet hours
        var settings = await _db.NotificationSettings.FindAsync(email);
        if (IsInQuietHours(settings)) return;

        if (await IsDuplicate(email, "overload", null, null))
            return;

        _db.Notifications.Add(new Notification
        {
            Email = email,
            Type = "overload",
            Title = "High Stress Alert",
            Message = $"Your stress level is at {stressScore:F0}%. Consider rescheduling or asking for help.",
            CreatedAt = DateTime.Now,
            RelatedEntityType = "Stress"
        });

        await _db.SaveChangesAsync();
    }

    public async Task CreateSharedTaskInviteNotificationAsync(string recipientEmail, string senderName, int taskId, string taskTitle)
    {
        // Check quiet hours
        var settings = await _db.NotificationSettings.FindAsync(recipientEmail);
        if (IsInQuietHours(settings)) return;

        _db.Notifications.Add(new Notification
        {
            Email = recipientEmail,
            Type = "shared_task_invite",
            Title = "Shared Task Invitation",
            Message = $"{senderName} invited you to collaborate on \"{taskTitle}\".",
            CreatedAt = DateTime.Now,
            RelatedEntityId = taskId,
            RelatedEntityType = "SharedTask"
        });

        await _db.SaveChangesAsync();
    }

    public async Task CreateSharedTaskResponseNotificationAsync(string recipientEmail, string responderName, int taskId, string taskTitle, bool accepted)
    {
        // Check quiet hours
        var settings = await _db.NotificationSettings.FindAsync(recipientEmail);
        if (IsInQuietHours(settings)) return;

        _db.Notifications.Add(new Notification
        {
            Email = recipientEmail,
            Type = "shared_task_response",
            Title = accepted ? "Task Accepted" : "Task Declined",
            Message = $"{responderName} {(accepted ? "accepted" : "declined")} the shared task \"{taskTitle}\".",
            CreatedAt = DateTime.Now,
            RelatedEntityId = taskId,
            RelatedEntityType = "SharedTask"
        });

        await _db.SaveChangesAsync();
    }

    public async Task GenerateDailySummaryAsync(string email)
    {
        var settings = await _db.NotificationSettings.FindAsync(email);
        if (settings == null || !settings.DailyMorningSummary) return;
        if (IsInQuietHours(settings)) return;

        // Only generate once per day
        if (await IsDuplicate(email, "daily_summary", null, null))
            return;

        var now = DateTime.Now;
        var todayEnd = now.Date.AddDays(1);

        var todayTasks = await _db.Tasks
            .Include(t => t.Course)
            .Where(t => t.Email == email && !t.IsCompleted
                && t.DueDate.HasValue && t.DueDate <= todayEnd.AddDays(1))
            .OrderBy(t => t.DueDate)
            .Take(5)
            .ToListAsync();

        var taskCount = todayTasks.Count;
        if (taskCount == 0)
        {
            _db.Notifications.Add(new Notification
            {
                Email = email,
                Type = "daily_summary",
                Title = "Good Morning!",
                Message = "No urgent tasks today. Keep up the great work!",
                CreatedAt = now,
            });
        }
        else
        {
            var taskNames = string.Join(", ", todayTasks.Select(t => t.Title).Take(3));
            var more = taskCount > 3 ? $" and {taskCount - 3} more" : "";
            _db.Notifications.Add(new Notification
            {
                Email = email,
                Type = "daily_summary",
                Title = "Daily Summary",
                Message = $"You have {taskCount} upcoming task(s): {taskNames}{more}.",
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task GenerateWeeklyPlanReminderAsync(string email)
    {
        var settings = await _db.NotificationSettings.FindAsync(email);
        if (settings == null || !settings.WeeklyPlanReminder) return;
        if (IsInQuietHours(settings)) return;

        // Only generate once per week (use 6-day window to avoid re-trigger)
        var sixDaysAgo = DateTime.Now.AddDays(-6);
        var exists = await _db.Notifications.AnyAsync(n =>
            n.Email == email && n.Type == "weekly_reminder" && n.CreatedAt > sixDaysAgo);
        if (exists) return;

        var now = DateTime.Now;
        var weekEnd = now.Date.AddDays(7);

        var weekTasks = await _db.Tasks
            .Where(t => t.Email == email && !t.IsCompleted
                && t.DueDate.HasValue && t.DueDate <= weekEnd)
            .CountAsync();

        var weekExams = await _db.Exams
            .Where(e => e.Course.UserCourses.Any(uc => uc.Email == email)
                && e.Date >= now.Date && e.Date <= weekEnd
                && e.IsTakingExam)
            .CountAsync();

        _db.Notifications.Add(new Notification
        {
            Email = email,
            Type = "weekly_reminder",
            Title = "Weekly Plan Reminder",
            Message = $"This week: {weekTasks} task(s) due{(weekExams > 0 ? $" and {weekExams} exam(s)" : "")}. Review your schedule and plan ahead!",
            CreatedAt = now,
        });

        await _db.SaveChangesAsync();
    }

    public async Task CreateNoCommonTimeNotificationAsync(string recipientEmail, int taskId, string taskTitle)
    {
        var settings = await _db.NotificationSettings.FindAsync(recipientEmail);
        if (IsInQuietHours(settings)) return;

        _db.Notifications.Add(new Notification
        {
            Email = recipientEmail,
            Type = "shared_task_no_time",
            Title = "No Common Time Found",
            Message = $"Could not find a common study time for \"{taskTitle}\". The task has been scheduled independently for each of you.",
            CreatedAt = DateTime.Now,
            RelatedEntityId = taskId,
            RelatedEntityType = "SharedTask"
        });

        await _db.SaveChangesAsync();
    }

    private bool IsInQuietHours(NotificationSettings? settings)
    {
        if (settings == null) return false;
        if (!settings.QuietHoursStart.HasValue || !settings.QuietHoursEnd.HasValue) return false;

        var now = DateTime.Now.TimeOfDay;
        var start = settings.QuietHoursStart.Value;
        var end = settings.QuietHoursEnd.Value;

        // Handle overnight quiet hours (e.g. 22:00 - 07:00)
        if (start > end)
            return now >= start || now <= end;

        return now >= start && now <= end;
    }

    private async Task<bool> IsDuplicate(string email, string type, int? entityId, string? entityType)
    {
        var since = DateTime.Now.AddHours(-24);
        return await _db.Notifications.AnyAsync(n =>
            n.Email == email && n.Type == type
            && n.CreatedAt > since
            && n.RelatedEntityId == entityId
            && n.RelatedEntityType == entityType);
    }
}
