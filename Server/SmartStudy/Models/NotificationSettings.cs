using SmartStudy.DAL;

namespace SmartStudy.Models;

// Per-user notification preferences (toggles + quiet-hours window).
public class NotificationSettings
{
    public string Email { get; set; } = null!;
    public bool NotifyBeforeTask { get; set; }
    public bool DailyMorningSummary { get; set; }
    public bool WeeklyPlanReminder { get; set; }
    public bool EnablePushNotification { get; set; }
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }

    // Navigation property
    public User User { get; set; } = null!;

    // ───── Static BLL methods ──────────────────────────────────

    // Loads the user's saved notification settings, or null if not yet created.
    public static NotificationSettings? GetByEmail(string email)
    {
        DBservices db = new DBservices();
        return db.GetNotifSettingsByEmail(email);
    }

    // Inserts or updates the user's notification settings row.
    public static void Upsert(string email, bool notifyBeforeTask, bool dailyMorningSummary,
        bool weeklyPlanReminder, bool enablePushNotification, TimeSpan? quietStart, TimeSpan? quietEnd)
    {
        DBservices db = new DBservices();
        db.UpsertNotifSettings(email, notifyBeforeTask, dailyMorningSummary, weeklyPlanReminder,
            enablePushNotification, quietStart, quietEnd);
    }

    // Inserts a default-toggles row for a brand-new user.
    public static void CreateDefault(string email)
    {
        DBservices db = new DBservices();
        db.CreateDefaultNotifSettings(email);
    }
}

// ───── DTO (from DashboardDtos.cs) ─────────────────────────────────

// Wire-format mirror of NotificationSettings with string time fields.
public class NotificationSettingsDto
{
    public bool NotifyBeforeTask { get; set; }
    public bool DailyMorningSummary { get; set; }
    public bool WeeklyPlanReminder { get; set; }
    public bool EnablePushNotification { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
}
