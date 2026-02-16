using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

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

        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        double sleepHours = prefs?.SleepHoursPerDay ?? 8.0;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        var incompleteTasks = await _db.Tasks
            .Include(t => t.SubTasks)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate != null)
            .ToListAsync();
        incompleteTasks = incompleteTasks.Where(t => !t.SubTasks.Any()).ToList();

        var userCourseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var upcomingExams = await _db.Exams
            .Where(e => userCourseIds.Contains(e.CourseId) && e.Date >= now.Date)
            .ToListAsync();

        // ML-adjusted hours
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
                if (courseRatios.TryGetValue(t.CourseId, out var ratio))
                    return est * ratio;
                return est;
            });

        requiredHours += upcomingExams
            .Where(e => (e.Date - now.Date).TotalDays <= 14)
            .Count() * 10.0;

        // Available hours until nearest deadline
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

            var events = await _db.Events
                .Where(e => e.Email == email && e.From > now && e.From < nearestDeadline.Value)
                .ToListAsync();
            double eventHours = events.Sum(e => (e.To - e.From).TotalHours);

            availableHours = Math.Max(1, totalHoursUntilDeadline - sleepTotal - eventHours);
        }
        else
        {
            availableHours = 168;
        }

        double score = requiredHours > 0
            ? Math.Min(100, (requiredHours / availableHours) * 100)
            : 0;

        // Calculate today's study and total load for the DTO
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);
        var todayTaskEvents = await _db.TaskEvents
            .Where(te => te.StudentTask.Email == email
                && te.From >= todayStart && te.From < todayEnd
                && (te.Status == "Scheduled" || te.Status == "Partial"))
            .ToListAsync();
        var todayOtherEvents = await _db.Events
            .Where(e => e.Email == email && e.From >= todayStart && e.From < todayEnd)
            .ToListAsync();

        double todayStudyHours = todayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
        double todayOtherHours = todayOtherEvents
            .Where(e => !todayTaskEvents.Any(te => te.EventId == e.EventId))
            .Sum(e => (e.To - e.From).TotalHours);
        double todayTotalHours = todayStudyHours + todayOtherHours;

        double studyLoad = maxDailyStudy > 0 ? (todayStudyHours / maxDailyStudy) * 100 : 0;
        double totalLoad = maxDailyTotal > 0 ? (todayTotalHours / maxDailyTotal) * 100 : 0;

        return new StressScoreDto
        {
            Score = Math.Round(score, 1),
            Level = GetStressLevel(score),
            Color = GetStressColor(score),
            RequiredHours = Math.Round(requiredHours, 1),
            AvailableHours = Math.Round(availableHours, 1),
            StudyLoad = Math.Round(Math.Min(100, studyLoad), 1),
            TotalLoad = Math.Round(Math.Min(100, totalLoad), 1),
            TotalScheduledHours = Math.Round(todayTotalHours, 1)
        };
    }

    public async Task<List<WeeklyStressDto>> GetWeeklyStressAsync(string email)
    {
        var now = DateTime.Now;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var result = new List<WeeklyStressDto>();

        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

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

            // Get scheduled task events for this day
            var dayTaskEvents = await _db.TaskEvents
                .Where(te => te.StudentTask.Email == email
                    && te.From >= day && te.From < dayEnd
                    && (te.Status == "Scheduled" || te.Status == "Partial"))
                .ToListAsync();

            var dayExams = await _db.Exams
                .Where(e => userCourseIds.Contains(e.CourseId)
                    && e.Date >= day && e.Date < dayEnd)
                .CountAsync();

            // Calculate dual load
            double studyHours = dayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
            double otherHours = dayEvents
                .Where(e => !dayTaskEvents.Any(te => te.EventId == e.EventId))
                .Sum(e => (e.To - e.From).TotalHours);
            double totalHours = studyHours + otherHours;

            double studyLoad = maxDailyStudy > 0 ? (studyHours / maxDailyStudy) * 100 : 0;
            double totalLoad = maxDailyTotal > 0 ? (totalHours / maxDailyTotal) * 100 : 0;
            double dayScore = Math.Min(100, Math.Max(studyLoad, totalLoad));

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
