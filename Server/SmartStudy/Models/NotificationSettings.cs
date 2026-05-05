using SmartStudy.DAL;

namespace SmartStudy.Models;

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

    public static NotificationSettings? GetByEmail(string email)
    {
        DBservices db = new DBservices();
        return db.GetNotifSettingsByEmail(email);
    }

    public static void Upsert(string email, bool notifyBeforeTask, bool dailyMorningSummary,
        bool weeklyPlanReminder, bool enablePushNotification, TimeSpan? quietStart, TimeSpan? quietEnd)
    {
        DBservices db = new DBservices();
        db.UpsertNotifSettings(email, notifyBeforeTask, dailyMorningSummary, weeklyPlanReminder,
            enablePushNotification, quietStart, quietEnd);
    }

    public static void CreateDefault(string email)
    {
        DBservices db = new DBservices();
        db.CreateDefaultNotifSettings(email);
    }
}

// ───── DTO (from DashboardDtos.cs) ─────────────────────────────────

public class NotificationSettingsDto
{
    public bool NotifyBeforeTask { get; set; }
    public bool DailyMorningSummary { get; set; }
    public bool WeeklyPlanReminder { get; set; }
    public bool EnablePushNotification { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
}
