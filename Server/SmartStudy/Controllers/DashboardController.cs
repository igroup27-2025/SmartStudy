using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;
using UserModel = SmartStudy.Models.User;
using SchedPrefsModel = SmartStudy.Models.SchedulingPreferences;

namespace SmartStudy.Controllers;

// API endpoints for the home dashboard view (stress, workload, suggestions).
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Aggregates stress, deadlines, exams, today's events, workload, and a suggested task.
    [HttpGet]
    public IActionResult Get()
    {
        var email = GetEmail();
        var now = DateTime.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        var stress = UserModel.GetStressScore(email);

        var courseIds = Dashboard.GetCourseIdsByEmail(email);

        var allTasks = StudentTask.GetByUser(email);

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

        var exams = Dashboard.GetUpcomingExams(email, today)
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

        var todayTypedEvents = Event.GetAllTypedEventsInRange(email, today, tomorrow);
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
                Description = e.Description,
                TaskId = e.TaskId,
                TaskTitle = e.TaskTitle,
                Status = e.Status,
                IsManuallyPinned = e.IsManuallyPinned,
                IsShared = e.IsShared,
                SharedStatus = e.SharedStatus
            })
            .ToList();

        var schedulingStatus = StudentTask.GetSchedulingStatus(email);

        var (incompleteTasks, taskEventsForTasks) = Dashboard.GetIncompleteTasksWithEvents(email);
        var taskEventsByTaskId = taskEventsForTasks.GroupBy(te => te.TaskId).ToDictionary(g => g.Key, g => g.ToList());

        var unscheduledCount = incompleteTasks.Count(t => !taskEventsByTaskId.ContainsKey(t.TaskId) || !taskEventsByTaskId[t.TaskId].Any());

        var todayWorkload = schedulingStatus.DailyWorkload
            .FirstOrDefault(d => d.Date.Date == today);

        var weeklyWorkload = schedulingStatus.DailyWorkload
            .Where(d => d.Date >= today && d.Date < today.AddDays(7))
            .Sum(d => d.ScheduledHours);

        var needsReviewTasks = incompleteTasks
            .Where(t =>
            {
                var events = taskEventsByTaskId.GetValueOrDefault(t.TaskId, new());
                var isOverdue = t.DueDate.HasValue && t.DueDate < now;
                return isOverdue
                    || events.Any(te => te.From < now)
                    || events.Any(te => te.Status == "NeedReview");
            })
            .OrderBy(t => t.DueDate)
            .Take(20)
            .Select(t =>
            {
                var events = taskEventsByTaskId.GetValueOrDefault(t.TaskId, new());
                var isOverdue = t.DueDate.HasValue && t.DueDate < now;
                string status;
                if (isOverdue) status = "Overdue";
                else if (events.Any(te => te.Status == "NeedReview")) status = "NeedReview";
                else if (events.Any()) status = "Scheduled";
                else status = "Unscheduled";

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
                    SchedulingStatus = status,
                    ScheduledSlots = events.OrderBy(te => te.From)
                        .Select(te => new TaskSlotDto { From = te.From, To = te.To })
                        .ToList()
                };
            })
            .ToList();

        var overdueTasks = needsReviewTasks
            .Where(t => t.SchedulingStatus == "Overdue")
            .ToList();

        var prefs = SchedPrefsModel.GetByEmail(email);
        var completedForML = Dashboard.GetCompletedTasksForML(email);
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

    // Returns a per-day list of suggested tasks to work on this week.
    [HttpGet("weekly-suggestions")]
    public IActionResult GetWeeklySuggestions()
    {
        var email = GetEmail();
        var suggestions = StudentTask.GetWeeklySuggestions(email);
        return Ok(suggestions);
    }
}
