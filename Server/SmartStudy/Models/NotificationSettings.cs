namespace SmartStudy.Models;

public class NotificationSettings
{
    public string Email { get; set; } = null!;
    public bool NotifyBeforeTask { get; set; }
    public bool DailyMorningSummary { get; set; }
    public bool WeeklyPlanReminder { get; set; }
    public bool EnablePushNotification { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
