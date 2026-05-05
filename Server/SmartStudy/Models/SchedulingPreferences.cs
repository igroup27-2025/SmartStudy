using SmartStudy.DAL;

namespace SmartStudy.Models;

public class SchedulingPreferences
{
    public string Email { get; set; } = null!;
    public double MaxDailyStudyHours { get; set; } = 6.0;
    public int MaxContinuousMinutes { get; set; } = 90;
    public int DayStartHour { get; set; } = 8;
    public int DayEndHour { get; set; } = 22;
    public double SleepHoursPerDay { get; set; } = 8.0;
    public TimeSpan? LunchBreakStart { get; set; }
    public TimeSpan? LunchBreakEnd { get; set; }
    public int BreakDurationMinutes { get; set; } = 15;
    public double DefaultTaskEstimatedHours { get; set; } = 4.0;
    public double MaxDailyTotalHours { get; set; } = 14.0;
    public double ExamPrepHoursPerDay { get; set; } = 5.0;
    public int ExamPrepDays { get; set; } = 3;

    // Navigation property
    public User User { get; set; } = null!;

    // ───── Static BLL methods ──────────────────────────────────

    public static SchedulingPreferences? GetByEmail(string email)
    {
        DBservices db = new DBservices();
        return db.GetSchedPrefsByEmail(email);
    }

    public static void Upsert(SchedulingPreferences prefs)
    {
        DBservices db = new DBservices();
        db.UpsertSchedPrefs(prefs);
    }
}

// ───── DTOs (from DashboardDtos.cs) ────────────────────────────────

public class SchedulingPreferencesDto
{
    public double MaxDailyStudyHours { get; set; }
    public int MaxContinuousMinutes { get; set; }
    public int DayStartHour { get; set; }
    public int DayEndHour { get; set; }
    public double SleepHoursPerDay { get; set; }
    public string? LunchBreakStart { get; set; }
    public string? LunchBreakEnd { get; set; }
    public int BreakDurationMinutes { get; set; } = 15;
    public double DefaultTaskEstimatedHours { get; set; } = 4.0;
    public double MaxDailyTotalHours { get; set; } = 14.0;
    public double ExamPrepHoursPerDay { get; set; } = 5.0;
    public int ExamPrepDays { get; set; } = 3;
}

public class OnboardingDto
{
    public SchedulingPreferencesDto? SchedulingPreferences { get; set; }
    public NotificationSettingsDto? NotificationSettings { get; set; }
    public List<OnboardingConstraintDto>? Constraints { get; set; }
}

public class OnboardingConstraintDto
{
    public string Type { get; set; } = "work";
    public string? Name { get; set; }
    public List<int> Days { get; set; } = new();
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
}
