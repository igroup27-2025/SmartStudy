using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DBservices _dal;
    private readonly StressService _stressService;
    private readonly SchedulingService _schedulingService;
    private readonly WeeklySuggestionService _weeklySuggestionService;

    public DashboardController(DBservices dal, StressService stressService, SchedulingService schedulingService, WeeklySuggestionService weeklySuggestionService)
    {
        _dal = dal;
        _stressService = stressService;
        _schedulingService = schedulingService;
        _weeklySuggestionService = weeklySuggestionService;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var email = GetEmail();
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        var stress = await _stressService.GetStressScoreAsync(email);

        var courseIds = await _dal.GetCourseIdsByEmailAsync(email);

        // Get all tasks for the user (completed and incomplete)
        var allTasks = await _dal.GetTasksByUserAsync(email);

        var upcomingDeadlines = allTasks
            .Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate > now)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .Select(t => new TaskDto
            {
                TaskId = t.TaskId,
                CourseId = t.CourseId,
                CourseName = t.CourseName,
                Title = t.Title,
                Type = t.Type,
                EstimatedHours = t.EstimatedHours,
                DueDate = t.DueDate,
                IsCompleted = t.IsCompleted,
                Priority = t.Priority
            })
            .ToList();

        var exams = (await _dal.GetUpcomingExamsAsync(email, today))
            .OrderBy(e => e.Date)
            .Take(3)
            .Select(e => new ExamDto
            {
                ExamId = e.ExamId,
                CourseId = e.CourseId,
                CourseName = e.CourseName,
                Date = e.Date,
                Time = e.Time,
                Session = e.Session,
                Duration = e.Duration,
                DaysUntil = (int)(e.Date - today).TotalDays
            })
            .ToList();

        // Today's events via typed events
        var todayTypedEvents = await _dal.GetAllTypedEventsInRangeAsync(email, today, tomorrow);
        var todayEventDtos = todayTypedEvents
            .OrderBy(e => e.From)
            .Select(e => new EventDto
            {
                EventId = e.EventId,
                From = e.From,
                To = e.To,
                Recurring = e.Recurring,
                EventType = e.EventType,
                CourseName = e.CourseName,
                Location = e.Location,
                WorkPlace = e.WorkPlace,
                Type = e.Type,
                Description = e.Description
            })
            .ToList();

        // Scheduling data
        var schedulingStatus = await _schedulingService.GetSchedulingStatusAsync(email);

        // Get incomplete tasks with their events
        var (incompleteTasks, taskEventsForTasks) = await _dal.GetIncompleteTasksWithEventsAsync(email);
        var taskEventsByTaskId = taskEventsForTasks.GroupBy(te => te.TaskId).ToDictionary(g => g.Key, g => g.ToList());

        var unscheduledCount = incompleteTasks.Count(t => !taskEventsByTaskId.ContainsKey(t.TaskId) || !taskEventsByTaskId[t.TaskId].Any());

        // Today's workload
        var todayWorkload = schedulingStatus.DailyWorkload
            .FirstOrDefault(d => d.Date.Date == today);

        // Weekly workload (next 7 days)
        var weeklyWorkload = schedulingStatus.DailyWorkload
            .Where(d => d.Date >= today && d.Date < today.AddDays(7))
            .Sum(d => d.ScheduledHours);

        // Needs Review: tasks with past scheduled events OR shared tasks awaiting review
        var needsReviewTasks = incompleteTasks
            .Where(t =>
            {
                var events = taskEventsByTaskId.GetValueOrDefault(t.TaskId, new());
                return events.Any(te => te.From < now) || events.Any(te => te.Status == "NeedReview");
            })
            .OrderBy(t => t.DueDate)
            .Take(10)
            .Select(t =>
            {
                var events = taskEventsByTaskId.GetValueOrDefault(t.TaskId, new());
                return new TaskDto
                {
                    TaskId = t.TaskId,
                    CourseId = t.CourseId,
                    CourseName = t.CourseName,
                    Title = t.Title,
                    Type = t.Type,
                    EstimatedHours = t.EstimatedHours,
                    DueDate = t.DueDate,
                    IsCompleted = t.IsCompleted,
                    Priority = t.Priority,
                    IsShared = t.SharedStatus != null,
                    SharedStatus = t.SharedStatus,
                    ScheduledDate = events.OrderBy(te => te.From).FirstOrDefault()?.From,
                    SchedulingStatus = events.Any(te => te.Status == "NeedReview") ? "NeedReview"
                        : events.Any() ? "Scheduled" : "Unscheduled",
                    ScheduledSlots = events.OrderBy(te => te.From)
                        .Select(te => new TaskSlotDto { From = te.From, To = te.To })
                        .ToList()
                };
            })
            .ToList();

        // Overdue tasks: due date in the past, not completed
        var overdueTasks = allTasks
            .Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate < now)
            .OrderBy(t => t.DueDate)
            .Select(t => new TaskDto
            {
                TaskId = t.TaskId,
                CourseId = t.CourseId,
                CourseName = t.CourseName,
                Title = t.Title,
                Type = t.Type,
                EstimatedHours = t.EstimatedHours,
                DueDate = t.DueDate,
                IsCompleted = t.IsCompleted,
                Priority = t.Priority
            })
            .ToList();

        // Next suggested task
        var prefs = await _dal.GetSchedPrefsByEmailAsync(email);
        var completedForML = await _dal.GetCompletedTasksForMLAsync(email);
        var courseRatios = completedForML
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.ActualHours / t.EstimatedHours));

        TaskDto? nextSuggested = null;
        var suggestedTask = incompleteTasks
            .Where(t => t.DueDate.HasValue && t.DueDate > now)
            .OrderByDescending(t =>
            {
                var daysUntilDue = Math.Max(0.1, (t.DueDate!.Value - now).TotalDays);
                double hours;
                if (t.EstimatedHours.HasValue && t.EstimatedHours > 0)
                    hours = (double)t.EstimatedHours;
                else
                    hours = t.DefaultTaskEstimatedHours ?? prefs?.DefaultTaskEstimatedHours ?? 4.0;
                if (courseRatios.TryGetValue(t.CourseId, out var ratio))
                    hours *= ratio;

                var credits = (double)(t.CourseCredits ?? 3);
                var isShared = t.SharedStatus != null;
                return (1.0 / daysUntilDue) * 50
                     + Math.Min(hours, 10) * 5
                     + credits * 4
                     + (isShared ? 25 : 0);
            })
            .FirstOrDefault();

        if (suggestedTask != null)
        {
            var suggestedEvents = taskEventsByTaskId.GetValueOrDefault(suggestedTask.TaskId, new())
                .Where(te => te.Status == "Scheduled" || te.Status == "Partial").ToList();

            nextSuggested = new TaskDto
            {
                TaskId = suggestedTask.TaskId,
                CourseId = suggestedTask.CourseId,
                CourseName = suggestedTask.CourseName,
                Title = suggestedTask.Title,
                Type = suggestedTask.Type,
                EstimatedHours = suggestedTask.EstimatedHours,
                DueDate = suggestedTask.DueDate,
                IsCompleted = suggestedTask.IsCompleted,
                Priority = suggestedTask.Priority,
                ScheduledDate = suggestedEvents.OrderBy(te => te.From).FirstOrDefault()?.From,
                SchedulingStatus = suggestedEvents.Any() ? "Scheduled" : "Unscheduled"
            };
        }

        return Ok(new DashboardDto
        {
            Stress = stress,
            TotalTasks = allTasks.Count,
            CompletedTasks = allTasks.Count(t => t.IsCompleted),
            PendingTasks = allTasks.Count(t => !t.IsCompleted),
            UpcomingExams = exams.Count,
            TotalCourses = courseIds.Count,
            UpcomingDeadlines = upcomingDeadlines,
            NextExams = exams,
            TodayEvents = todayEventDtos,
            UnscheduledTaskCount = unscheduledCount,
            TodayWorkloadHours = todayWorkload?.ScheduledHours ?? 0,
            WeeklyWorkloadHours = Math.Round(weeklyWorkload, 1),
            DailyWorkload = schedulingStatus.DailyWorkload,
            OverloadedDays = schedulingStatus.OverloadedDays,
            NextSuggestedTask = nextSuggested,
            NeedsReviewTasks = needsReviewTasks,
            OverdueTasks = overdueTasks,
            RelocationSuggestions = schedulingStatus.RelocationSuggestions
        });
    }

    [HttpGet("weekly-suggestions")]
    public async Task<IActionResult> GetWeeklySuggestions()
    {
        var email = GetEmail();
        var suggestions = await _weeklySuggestionService.GetWeeklySuggestionsAsync(email);
        return Ok(suggestions);
    }
}
