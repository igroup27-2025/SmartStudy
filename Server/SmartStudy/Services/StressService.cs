using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;

namespace SmartStudy.Services;

public class StressService
{
    private readonly SmartStudyDbContext _db;

    public StressService(SmartStudyDbContext db)
    {
        _db = db;
    }

    public async Task<StressScoreDto> GetStressScoreAsync(string email)
    {
        var now = DateTime.Now;

        // Load user preferences
        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        double sleepHours = prefs?.SleepHoursPerDay ?? 8.0;

        var incompleteTasks = await _db.Tasks
            .Include(t => t.SubTasks)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate != null)
            .ToListAsync();
        // Only count leaf tasks to avoid double-counting parents
        incompleteTasks = incompleteTasks.Where(t => !t.SubTasks.Any()).ToList();

        var userCourseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var upcomingExams = await _db.Exams
            .Where(e => userCourseIds.Contains(e.CourseId) && e.Date >= now.Date)
            .ToListAsync();

        // ML-adjusted hours: apply per-course ratio from completed tasks
        var completedTasks = await _db.Tasks
            .Where(t => t.Email == email && t.IsCompleted && t.ActualHours.HasValue && t.EstimatedHours.HasValue && t.EstimatedHours > 0)
            .Select(t => new { t.CourseId, Actual = (double)t.ActualHours!.Value, Estimated = (double)t.EstimatedHours!.Value })
            .ToListAsync();

        var courseRatios = completedTasks
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.Actual / t.Estimated));

        double requiredHours = incompleteTasks
            .Where(t => t.EstimatedHours.HasValue)
            .Sum(t =>
            {
                var est = (double)t.EstimatedHours!.Value;
                // Apply ML ratio if available for this course
                if (courseRatios.TryGetValue(t.CourseId, out var ratio))
                    return est * ratio;
                return est;
            });

        // Add exam prep hours (assume 10 hours per exam within 14 days)
        requiredHours += upcomingExams
            .Where(e => (e.Date - now.Date).TotalDays <= 14)
            .Count() * 10.0;

        // Calculate available hours until nearest deadline
        DateTime? nearestDeadline = null;

        var taskDeadlines = incompleteTasks
            .Where(t => t.DueDate > now)
            .Select(t => t.DueDate!.Value);

        var examDeadlines = upcomingExams
            .Select(e => e.Date.Add(e.Time));

        var allDeadlines = taskDeadlines.Concat(examDeadlines).Where(d => d > now).ToList();

        if (allDeadlines.Count != 0)
            nearestDeadline = allDeadlines.Min();

        double availableHours;
        if (nearestDeadline.HasValue)
        {
            double totalHoursUntilDeadline = (nearestDeadline.Value - now).TotalHours;
            double daysUntilDeadline = totalHoursUntilDeadline / 24.0;
            double sleepTotal = daysUntilDeadline * sleepHours;

            // Subtract existing events from available hours
            var events = await _db.Events
                .Where(e => e.Email == email && e.From > now && e.From < nearestDeadline.Value)
                .ToListAsync();
            double eventHours = events.Sum(e => (e.To - e.From).TotalHours);

            availableHours = Math.Max(1, totalHoursUntilDeadline - sleepTotal - eventHours);
        }
        else
        {
            availableHours = 168; // One week of waking hours
        }

        double score = requiredHours > 0
            ? Math.Min(100, (requiredHours / availableHours) * 100)
            : 0;

        return new StressScoreDto
        {
            Score = Math.Round(score, 1),
            Level = GetStressLevel(score),
            Color = GetStressColor(score),
            RequiredHours = Math.Round(requiredHours, 1),
            AvailableHours = Math.Round(availableHours, 1)
        };
    }

    public async Task<List<WeeklyStressDto>> GetWeeklyStressAsync(string email)
    {
        var now = DateTime.Now;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var result = new List<WeeklyStressDto>();

        // Load user preferences
        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        double wakingHours = 24 - (prefs?.SleepHoursPerDay ?? 8.0);

        var userCourseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        for (int i = 0; i < 7; i++)
        {
            var day = startOfWeek.AddDays(i);
            var dayEnd = day.AddDays(1);

            var dayTasks = await _db.Tasks
                .Where(t => t.Email == email && !t.IsCompleted
                    && t.DueDate != null && t.DueDate >= day && t.DueDate < dayEnd)
                .CountAsync();

            var dayEvents = await _db.Events
                .Where(e => e.Email == email && e.From >= day && e.From < dayEnd)
                .ToListAsync();

            var dayExams = await _db.Exams
                .Where(e => userCourseIds.Contains(e.CourseId)
                    && e.Date >= day && e.Date < dayEnd)
                .CountAsync();

            // Subtract event hours from available time
            double eventHours = dayEvents.Sum(e => (e.To - e.From).TotalHours);
            double dayHours = dayTasks * 2.0 + dayExams * 10.0;
            double dayAvailable = Math.Max(1, wakingHours - eventHours);
            double dayScore = dayAvailable > 0
                ? Math.Min(100, (dayHours / dayAvailable) * 100)
                : 0;

            result.Add(new WeeklyStressDto
            {
                DayName = day.ToString("ddd"),
                Date = day,
                Score = Math.Round(dayScore, 1),
                Level = GetStressLevel(dayScore),
                Color = GetStressColor(dayScore),
                TaskCount = dayTasks,
                EventCount = dayEvents.Count
            });
        }

        return result;
    }

    private static string GetStressLevel(double score) => score switch
    {
        <= 40 => "Low",
        <= 70 => "Moderate",
        _ => "High"
    };

    private static string GetStressColor(double score) => score switch
    {
        <= 40 => "#27AE60",
        <= 70 => "#F28D35",
        _ => "#E74C3C"
    };
}
