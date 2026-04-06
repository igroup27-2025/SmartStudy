using SmartStudy.DAL;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class NotificationService
{
    private readonly DBservices _db;

    public NotificationService(DBservices db)
    {
        _db = db;
    }

    public async Task GenerateDeadlineNotificationsAsync(string email)
    {
        // Check if deadline notifications are enabled
        var settings = await _db.GetNotifSettingsByEmailAsync(email);
        if (settings != null && !settings.NotifyBeforeTask) return;

        // Check quiet hours
        if (IsInQuietHours(settings)) return;

        var urgentTasks = await _db.GetUpcomingDeadlineTasksAsync(email);

        foreach (var task in urgentTasks)
        {
            if (await _db.IsNotificationDuplicateAsync(email, "deadline", task.TaskId, "Task"))
                continue;

            await _db.CreateNotificationAsync(
                email,
                "deadline",
                "Task Due Soon",
                $"\"{task.Title}\" ({task.CourseName}) is due within 24 hours!",
                task.TaskId,
                "Task");
        }
    }

    public async Task GenerateOverloadNotificationAsync(string email, double stressScore)
    {
        if (stressScore <= 70) return;

        // Check quiet hours
        var settings = await _db.GetNotifSettingsByEmailAsync(email);
        if (IsInQuietHours(settings)) return;

        if (await _db.IsNotificationDuplicateAsync(email, "overload", null, null))
            return;

        await _db.CreateNotificationAsync(
            email,
            "overload",
            "High Stress Alert",
            $"Your stress level is at {stressScore:F0}%. Consider rescheduling or asking for help.",
            null,
            "Stress");
    }

    public async Task CreateSharedTaskInviteNotificationAsync(string recipientEmail, string senderName, int taskId, string taskTitle)
    {
        // Check quiet hours
        var settings = await _db.GetNotifSettingsByEmailAsync(recipientEmail);
        if (IsInQuietHours(settings)) return;

        await _db.CreateNotificationAsync(
            recipientEmail,
            "shared_task_invite",
            "Shared Task Invitation",
            $"{senderName} invited you to collaborate on \"{taskTitle}\".",
            taskId,
            "SharedTask");
    }

    public async Task CreateSharedTaskResponseNotificationAsync(string recipientEmail, string responderName, int taskId, string taskTitle, bool accepted)
    {
        // Check quiet hours
        var settings = await _db.GetNotifSettingsByEmailAsync(recipientEmail);
        if (IsInQuietHours(settings)) return;

        await _db.CreateNotificationAsync(
            recipientEmail,
            "shared_task_response",
            accepted ? "Task Accepted" : "Task Declined",
            $"{responderName} {(accepted ? "accepted" : "declined")} the shared task \"{taskTitle}\".",
            taskId,
            "SharedTask");
    }

    public async Task GenerateDailySummaryAsync(string email)
    {
        var settings = await _db.GetNotifSettingsByEmailAsync(email);
        if (settings == null || !settings.DailyMorningSummary) return;
        if (IsInQuietHours(settings)) return;

        // Only generate once per day
        if (await _db.IsNotificationDuplicateAsync(email, "daily_summary", null, null))
            return;

        var todayTasks = await _db.GetDailySummaryDataAsync(email);
        var taskCount = todayTasks.Count;

        if (taskCount == 0)
        {
            await _db.CreateNotificationAsync(
                email,
                "daily_summary",
                "Good Morning!",
                "No urgent tasks today. Keep up the great work!");
        }
        else
        {
            var taskNames = string.Join(", ", todayTasks.Select(t => t.Title).Take(3));
            var more = taskCount > 3 ? $" and {taskCount - 3} more" : "";
            await _db.CreateNotificationAsync(
                email,
                "daily_summary",
                "Daily Summary",
                $"You have {taskCount} upcoming task(s): {taskNames}{more}.");
        }
    }

    public async Task GenerateWeeklyPlanReminderAsync(string email)
    {
        var settings = await _db.GetNotifSettingsByEmailAsync(email);
        if (settings == null || !settings.WeeklyPlanReminder) return;
        if (IsInQuietHours(settings)) return;

        // Only generate once per week (use 6-day window to avoid re-trigger)
        if (await _db.HasRecentWeeklyReminderAsync(email))
            return;

        var (weekTasks, weekExams) = await _db.GetWeeklyPlanDataAsync(email);

        await _db.CreateNotificationAsync(
            email,
            "weekly_reminder",
            "Weekly Plan Reminder",
            $"This week: {weekTasks} task(s) due{(weekExams > 0 ? $" and {weekExams} exam(s)" : "")}. Review your schedule and plan ahead!");
    }

    public async Task CreateNoCommonTimeNotificationAsync(string recipientEmail, int taskId, string taskTitle)
    {
        var settings = await _db.GetNotifSettingsByEmailAsync(recipientEmail);
        if (IsInQuietHours(settings)) return;

        await _db.CreateNotificationAsync(
            recipientEmail,
            "shared_task_no_time",
            "No Common Time Found",
            $"Could not find a common study time for \"{taskTitle}\". The task has been scheduled independently for each of you.",
            taskId,
            "SharedTask");
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
}
