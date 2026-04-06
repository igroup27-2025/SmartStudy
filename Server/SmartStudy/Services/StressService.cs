using SmartStudy.DAL;
using SmartStudy.DTOs;

namespace SmartStudy.Services;

public class StressService
{
    private readonly DBservices _dal;

    public StressService(DBservices dal)
    {
        _dal = dal;
    }

    public async Task<StressScoreDto> GetStressScoreAsync(string email)
    {
        var now = DateTime.Now;

        var prefs = await _dal.GetSchedPrefsByEmailAsync(email);
        double sleepHours = prefs?.SleepHoursPerDay ?? 8.0;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        // Get incomplete leaf tasks
        var allLeafTasks = await _dal.GetIncompleteLeafTasksAsync(email);
        var incompleteTasks = allLeafTasks.Where(t => t.DueDate != null).ToList();

        var courseIds = await _dal.GetCourseIdsByEmailAsync(email);

        var upcomingExams = await _dal.GetUpcomingExamsAsync(email, now.Date);

        // ML-adjusted hours
        var completedForML = await _dal.GetCompletedTasksForMLAsync(email);

        var courseRatios = completedForML
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.ActualHours / t.EstimatedHours));

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

            var events = await _dal.GetBaseEventsInRangeOrRecurringAsync(email, now, nearestDeadline.Value);
            // Only count events actually in the range (not recurring originals outside range)
            double eventHours = events
                .Where(e => e.From > now && e.From < nearestDeadline.Value && !e.Recurring)
                .Sum(e => (e.To - e.From).TotalHours);

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

        var todayTaskEvents = await _dal.GetTaskEventsInRangeAsync(email, todayStart, todayEnd);
        todayTaskEvents = todayTaskEvents
            .Where(te => te.Status == "Scheduled" || te.Status == "Partial")
            .ToList();

        var todayAllEvents = await _dal.GetEventsInDateRangeAsync(email, todayStart, todayEnd);

        double todayStudyHours = todayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
        double todayOtherHours = todayAllEvents
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

        var prefs = await _dal.GetSchedPrefsByEmailAsync(email);
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        // Preload data for the entire week
        var weekEnd = startOfWeek.AddDays(7);
        var allLeafTasks = await _dal.GetIncompleteLeafTasksAsync(email);
        var allEvents = await _dal.GetEventsInDateRangeAsync(email, startOfWeek, weekEnd);
        var allTaskEvents = await _dal.GetTaskEventsInRangeAsync(email, startOfWeek, weekEnd);
        var upcomingExams = await _dal.GetUpcomingExamsAsync(email, startOfWeek);

        for (int i = 0; i < 7; i++)
        {
            var day = startOfWeek.AddDays(i);
            var dayEnd = day.AddDays(1);

            var dayTasks = allLeafTasks
                .Count(t => t.DueDate != null && t.DueDate >= day && t.DueDate < dayEnd);

            var dayEvents = allEvents
                .Where(e => e.From >= day && e.From < dayEnd)
                .ToList();

            var dayTaskEvents = allTaskEvents
                .Where(te => te.From >= day && te.From < dayEnd
                    && (te.Status == "Scheduled" || te.Status == "Partial"))
                .ToList();

            var dayExams = upcomingExams
                .Count(e => e.Date >= day && e.Date < dayEnd);

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
