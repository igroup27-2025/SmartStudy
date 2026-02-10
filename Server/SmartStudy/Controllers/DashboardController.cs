using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly StressService _stressService;
    private readonly SchedulingService _schedulingService;

    public DashboardController(SmartStudyDbContext db, StressService stressService, SchedulingService schedulingService)
    {
        _db = db;
        _stressService = stressService;
        _schedulingService = schedulingService;
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

        var courseIds = await _db.UserCourses
            .Where(uc => uc.Email == email)
            .Select(uc => uc.CourseId)
            .ToListAsync();

        var tasks = await _db.Tasks.Include(t => t.Course)
            .Where(t => t.Email == email)
            .ToListAsync();

        var upcomingDeadlines = tasks
            .Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate > now)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .Select(t => new TaskDto
            {
                TaskId = t.TaskId,
                CourseId = t.CourseId,
                CourseName = t.Course.CourseName,
                Title = t.Title,
                Type = t.Type,
                EstimatedHours = t.EstimatedHours,
                DueDate = t.DueDate,
                IsCompleted = t.IsCompleted,
                Priority = t.Priority
            })
            .ToList();

        var exams = await _db.Exams.Include(e => e.Course)
            .Where(e => courseIds.Contains(e.CourseId) && e.Date >= today)
            .OrderBy(e => e.Date)
            .Take(3)
            .Select(e => new ExamDto
            {
                ExamId = e.ExamId,
                CourseId = e.CourseId,
                CourseName = e.Course.CourseName,
                Date = e.Date,
                Time = e.Time,
                Session = e.Session,
                Duration = e.Duration,
                DaysUntil = (int)(e.Date - today).TotalDays
            })
            .ToListAsync();

        var todayEvents = await _db.Events
            .Where(e => e.Email == email && e.From >= today && e.From < tomorrow)
            .OrderBy(e => e.From)
            .ToListAsync();

        var todayEventDtos = new List<EventDto>();
        foreach (var evt in todayEvents)
        {
            var dto = new EventDto
            {
                EventId = evt.EventId,
                From = evt.From,
                To = evt.To,
                Recurring = evt.Recurring
            };

            var ce = await _db.ClassEvents.Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.EventId == evt.EventId);
            if (ce != null) { dto.EventType = "class"; dto.CourseName = ce.Course.CourseName; dto.Location = ce.Location; }
            else
            {
                var we = await _db.WorkEvents.FirstOrDefaultAsync(w => w.EventId == evt.EventId);
                if (we != null) { dto.EventType = "work"; dto.WorkPlace = we.WorkPlace; }
                else
                {
                    var pe = await _db.PersonalEvents.FirstOrDefaultAsync(p => p.EventId == evt.EventId);
                    if (pe != null) { dto.EventType = "personal"; dto.Type = pe.Type; dto.Description = pe.Description; }
                    else dto.EventType = "event";
                }
            }
            todayEventDtos.Add(dto);
        }

        // Scheduling data
        var schedulingStatus = await _schedulingService.GetSchedulingStatusAsync(email);

        var incompleteTasks = tasks.Where(t => !t.IsCompleted).ToList();
        var tasksWithEvents = await _db.Tasks.Include(t => t.TaskEvents).Include(t => t.Course)
            .Where(t => t.Email == email && !t.IsCompleted)
            .ToListAsync();

        var unscheduledCount = tasksWithEvents.Count(t => !t.TaskEvents.Any());

        // Today's workload
        var todayWorkload = schedulingStatus.DailyWorkload
            .FirstOrDefault(d => d.Date.Date == today);

        // Weekly workload (next 7 days)
        var weeklyWorkload = schedulingStatus.DailyWorkload
            .Where(d => d.Date >= today && d.Date < today.AddDays(7))
            .Sum(d => d.ScheduledHours);

        // Next suggested task: highest priority, soonest deadline, not completed
        TaskDto? nextSuggested = null;
        var suggested = tasksWithEvents
            .Where(t => t.DueDate.HasValue && t.DueDate > now)
            .OrderByDescending(t => t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : 1)
            .ThenBy(t => t.DueDate)
            .FirstOrDefault();

        if (suggested != null)
        {
            var suggestedEvents = suggested.TaskEvents?
                .Where(te => te.Status == "Scheduled" || te.Status == "Partial").ToList()
                ?? new List<TaskEvent>();

            nextSuggested = new TaskDto
            {
                TaskId = suggested.TaskId,
                CourseId = suggested.CourseId,
                CourseName = suggested.Course?.CourseName ?? "",
                Title = suggested.Title,
                Type = suggested.Type,
                EstimatedHours = suggested.EstimatedHours,
                DueDate = suggested.DueDate,
                IsCompleted = suggested.IsCompleted,
                Priority = suggested.Priority,
                ScheduledDate = suggestedEvents.OrderBy(te => te.From).FirstOrDefault()?.From,
                SchedulingStatus = suggestedEvents.Any() ? "Scheduled" : "Unscheduled"
            };
        }

        return Ok(new DashboardDto
        {
            Stress = stress,
            TotalTasks = tasks.Count,
            CompletedTasks = tasks.Count(t => t.IsCompleted),
            PendingTasks = tasks.Count(t => !t.IsCompleted),
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
            NextSuggestedTask = nextSuggested
        });
    }
}
