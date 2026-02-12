namespace SmartStudy.DTOs;

public class DashboardDto
{
    public StressScoreDto Stress { get; set; } = null!;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int UpcomingExams { get; set; }
    public int TotalCourses { get; set; }
    public List<TaskDto> UpcomingDeadlines { get; set; } = new();
    public List<ExamDto> NextExams { get; set; } = new();
    public List<EventDto> TodayEvents { get; set; } = new();

    // Scheduling data
    public int UnscheduledTaskCount { get; set; }
    public double TodayWorkloadHours { get; set; }
    public double WeeklyWorkloadHours { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
    public TaskDto? NextSuggestedTask { get; set; }
}

public class StressScoreDto
{
    public double Score { get; set; }
    public string Level { get; set; } = null!;
    public string Color { get; set; } = null!;
    public double RequiredHours { get; set; }
    public double AvailableHours { get; set; }
}

public class WeeklyStressDto
{
    public string DayName { get; set; } = null!;
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public string Level { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int TaskCount { get; set; }
    public int EventCount { get; set; }
}

public class NotificationSettingsDto
{
    public bool NotifyBeforeTask { get; set; }
    public bool DailyMorningSummary { get; set; }
    public bool WeeklyPlanReminder { get; set; }
    public bool EnablePushNotification { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
}

public class UserProfileDto
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public NotificationSettingsDto? NotificationSettings { get; set; }
}

public class UpdateProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class SchedulingPreferencesDto
{
    public double MaxDailyStudyHours { get; set; }
    public int MaxContinuousMinutes { get; set; }
    public int DayStartHour { get; set; }
    public int DayEndHour { get; set; }
    public double SleepHoursPerDay { get; set; }
    public string? LunchBreakStart { get; set; }
    public string? LunchBreakEnd { get; set; }
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
