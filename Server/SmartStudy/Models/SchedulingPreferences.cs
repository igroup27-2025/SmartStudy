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
}
