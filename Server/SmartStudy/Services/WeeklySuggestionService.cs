using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;

namespace SmartStudy.Services;

public class WeeklySuggestionService
{
    private readonly SmartStudyDbContext _db;
    private readonly StressService _stressService;

    public WeeklySuggestionService(SmartStudyDbContext db, StressService stressService)
    {
        _db = db;
        _stressService = stressService;
    }

    public async Task<WeeklySuggestionsDto> GetWeeklySuggestionsAsync(string email)
    {
        var now = DateTime.Now;
        var weekEnd = now.Date.AddDays(7);
        var result = new WeeklySuggestionsDto();

        // Get upcoming tasks
        var tasks = await _db.Tasks
            .Include(t => t.Course)
            .Include(t => t.SubTasks)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate.HasValue && t.DueDate <= weekEnd.AddDays(7))
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        // Only count leaf tasks
        var leafTasks = tasks.Where(t => !t.SubTasks.Any()).ToList();

        var totalHoursNeeded = leafTasks.Sum(t => (double)(t.EstimatedHours ?? 1));
        result.TotalStudyHoursNeeded = Math.Round(totalHoursNeeded, 1);

        // Calculate available study hours (8 hours/day * 7 days, minus events)
        var events = await _db.Events
            .Where(e => e.Email == email && e.From >= now.Date && e.From < weekEnd)
            .ToListAsync();
        var busyHours = events.Sum(e => (e.To - e.From).TotalHours);
        var totalDayHours = 7 * 14.0; // 14 usable hours per day (8AM-10PM)
        result.AvailableStudyHours = Math.Round(Math.Max(0, totalDayHours - busyHours), 1);

        // Get stress
        var stress = await _stressService.GetStressScoreAsync(email);

        // Generate suggestions
        if (stress.Score > 70)
        {
            result.Suggestions.Add(new SuggestionDto
            {
                Type = "warning",
                Title = "High Stress Alert",
                Message = $"Your stress level is {stress.Score:F0}%. Consider redistributing tasks or asking a study partner for help.",
                Icon = "&#9888;"
            });
        }

        // Check for overloaded days
        var tasksByDay = leafTasks
            .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date >= now.Date && t.DueDate.Value.Date < weekEnd)
            .GroupBy(t => t.DueDate!.Value.Date)
            .Where(g => g.Sum(t => (double)(t.EstimatedHours ?? 1)) > 6)
            .ToList();

        if (tasksByDay.Any())
        {
            var dayNames = string.Join(", ", tasksByDay.Select(g => g.Key.ToString("ddd")));
            result.Suggestions.Add(new SuggestionDto
            {
                Type = "overload",
                Title = "Overloaded Days",
                Message = $"You have heavy workload on {dayNames}. Try to spread tasks across the week.",
                Icon = "&#128200;"
            });
        }

        // Available study time
        if (result.AvailableStudyHours > totalHoursNeeded * 1.5)
        {
            result.Suggestions.Add(new SuggestionDto
            {
                Type = "positive",
                Title = "Good Balance",
                Message = $"You have {result.AvailableStudyHours:F0}h available for {totalHoursNeeded:F0}h of work. You're on track!",
                Icon = "&#9989;"
            });
        }
        else if (result.AvailableStudyHours < totalHoursNeeded)
        {
            result.Suggestions.Add(new SuggestionDto
            {
                Type = "danger",
                Title = "Not Enough Time",
                Message = $"You need {totalHoursNeeded:F0}h but only have {result.AvailableStudyHours:F0}h available. Prioritize critical tasks!",
                Icon = "&#128308;"
            });
        }

        // Upcoming deadline warning
        var urgentTasks = leafTasks.Where(t => t.DueDate.HasValue && (t.DueDate.Value - now).TotalHours <= 48).ToList();
        if (urgentTasks.Any())
        {
            result.Suggestions.Add(new SuggestionDto
            {
                Type = "urgent",
                Title = "Urgent Deadlines",
                Message = $"{urgentTasks.Count} task(s) due within 48 hours!",
                Icon = "&#9200;"
            });
        }

        // Top 3 focus tasks (highest priority, soonest deadline)
        result.FocusTasks = leafTasks
            .Where(t => t.DueDate.HasValue && t.DueDate > now)
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : 1)
            .Take(3)
            .Select(t => new FocusTaskDto
            {
                TaskId = t.TaskId,
                Title = t.Title,
                CourseName = t.Course?.CourseName ?? "",
                HoursNeeded = (double)(t.EstimatedHours ?? 1),
                DaysUntilDue = Math.Max(0, (int)(t.DueDate!.Value.Date - now.Date).TotalDays),
                Priority = t.Priority
            })
            .ToList();

        return result;
    }
}
