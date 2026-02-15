using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class SchedulingService
{
    private readonly SmartStudyDbContext _db;

    private const int SlotMinutes = 30;

    public SchedulingService(SmartStudyDbContext db)
    {
        _db = db;
    }

    public async Task<SchedulingResultDto> ScheduleAllTasksAsync(string email)
    {
        var now = DateTime.Now;
        var result = new SchedulingResultDto();

        // Load user preferences
        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDaily = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxContinuous = (prefs?.MaxContinuousMinutes ?? 90) / 60.0;

        // 1. Get all incomplete tasks with due dates (only leaf tasks - no parents with children)
        // Include overdue tasks — treat them as urgent (immediate deadline)
        var allTasks = await _db.Tasks
            .Include(t => t.Course)
            .Include(t => t.TaskEvents)
            .Include(t => t.SubTasks)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate.HasValue)
            .ToListAsync();
        // Only schedule leaf tasks (no sub-tasks) to avoid double-counting
        var tasks = allTasks.Where(t => !t.SubTasks.Any()).ToList();

        // 2. Get all existing non-task events (class, work, personal) for the scheduling window
        var maxDueDate = tasks.Any() ? tasks.Max(t => t.DueDate!.Value) : now.AddDays(14);
        if (maxDueDate < now.AddDays(7)) maxDueDate = now.AddDays(7); // At least 7 days for overdue tasks
        var scheduleEnd = maxDueDate.Date.AddDays(1);
        var scheduleStart = now.Date;

        var allEvents = await _db.Events
            .Where(e => e.Email == email && e.To >= scheduleStart && e.From <= scheduleEnd)
            .ToListAsync();

        // Identify which events are auto-scheduled task events (to be cleared)
        var existingTaskEventIds = await _db.TaskEvents
            .Where(te => te.StudentTask.Email == email && te.Status == "Scheduled")
            .Select(te => te.EventId)
            .ToListAsync();

        // 3. Clear existing auto-scheduled TaskEvents
        if (existingTaskEventIds.Any())
        {
            var toRemove = await _db.Events
                .Where(e => existingTaskEventIds.Contains(e.EventId))
                .ToListAsync();
            _db.Events.RemoveRange(toRemove);
            await _db.SaveChangesAsync();
        }

        // Reload events without the cleared task events (include recurring for expansion)
        var fixedEvents = await GetExpandedEvents(email, scheduleStart, scheduleEnd);

        // Add lunch break as blocked slots if configured
        var lunchEvents = new List<Event>();
        if (prefs?.LunchBreakStart != null && prefs?.LunchBreakEnd != null)
        {
            for (var day = scheduleStart; day < scheduleEnd; day = day.AddDays(1))
            {
                lunchEvents.Add(new Event
                {
                    Email = email,
                    From = day.Add(prefs.LunchBreakStart.Value),
                    To = day.Add(prefs.LunchBreakEnd.Value),
                    Recurring = false
                });
            }
            fixedEvents.AddRange(lunchEvents);
        }

        // ML-adjusted hours: apply per-course ratio from completed tasks
        var completedForML = await _db.Tasks
            .Where(t => t.Email == email && t.IsCompleted && t.ActualHours.HasValue && t.EstimatedHours.HasValue && t.EstimatedHours > 0)
            .Select(t => new { t.CourseId, Actual = (double)t.ActualHours!.Value, Estimated = (double)t.EstimatedHours!.Value })
            .ToListAsync();

        var courseRatios = completedForML
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.Actual / t.Estimated));

        // 4. Sort tasks by priority score
        // Overdue tasks get the highest urgency (daysUntilDue is clamped to 0.1)
        var scoredTasks = tasks.Select(t =>
        {
            var daysUntilDue = Math.Max(0.1, (t.DueDate!.Value - now).TotalDays);
            var priorityWeight = t.Priority?.ToLower() switch
            {
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 2
            };
            var hours = (double)(t.EstimatedHours ?? 1);
            // Apply ML ratio if available
            if (courseRatios.TryGetValue(t.CourseId, out var ratio))
                hours *= ratio;
            var score = (1.0 / daysUntilDue) * 40 + priorityWeight * 30 + Math.Min(hours, 10) * 3;
            return new { Task = t, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Task)
        .ToList();

        // Track scheduled hours per day (to enforce maxDaily)
        var dailyScheduledHours = new Dictionary<DateTime, double>();
        var newTaskEvents = new List<TaskEvent>();

        // 5. For each task, greedily assign to earliest available slots
        foreach (var task in scoredTasks)
        {
            var totalHours = (double)(task.EstimatedHours ?? 1);
            // Apply ML ratio if available
            if (courseRatios.TryGetValue(task.CourseId, out var mlRatio))
                totalHours *= mlRatio;
            var remainingHours = totalHours;
            var slots = new List<ScheduledSlotDto>();
            // For overdue tasks, schedule them within the next 7 days
            var dueDate = task.DueDate!.Value < now ? now.Date.AddDays(7) : task.DueDate!.Value.Date;

            // Iterate day by day from today to due date
            for (var day = scheduleStart; day < dueDate && remainingHours > 0; day = day.AddDays(1))
            {
                var dayKey = day.Date;
                if (!dailyScheduledHours.ContainsKey(dayKey))
                    dailyScheduledHours[dayKey] = 0;

                var dayAvailable = maxDaily - dailyScheduledHours[dayKey];
                if (dayAvailable <= 0) continue;

                // Build free slots for this day
                var dayFreeSlots = GetFreeSlots(day, fixedEvents, newTaskEvents, dayStart, dayEnd);

                var taskHoursToday = 0.0;
                foreach (var freeSlot in dayFreeSlots)
                {
                    if (remainingHours <= 0) break;
                    if (dayAvailable - taskHoursToday <= 0) break;
                    if (taskHoursToday >= maxContinuous) break;

                    var slotDuration = (freeSlot.To - freeSlot.From).TotalHours;
                    var canUse = Math.Min(slotDuration,
                        Math.Min(remainingHours,
                        Math.Min(dayAvailable - taskHoursToday,
                        maxContinuous - taskHoursToday)));

                    if (canUse < 0.5) continue; // Skip slots smaller than 30 min

                    // Round to nearest 30 min
                    canUse = Math.Floor(canUse * 2) / 2;
                    if (canUse <= 0) continue;

                    var slotFrom = freeSlot.From;
                    var slotTo = slotFrom.AddHours(canUse);

                    var taskEvent = new TaskEvent
                    {
                        Email = email,
                        From = slotFrom,
                        To = slotTo,
                        Recurring = false,
                        TaskId = task.TaskId,
                        Priority = task.Priority,
                        Status = "Scheduled"
                    };
                    newTaskEvents.Add(taskEvent);

                    slots.Add(new ScheduledSlotDto { From = slotFrom, To = slotTo });
                    remainingHours -= canUse;
                    taskHoursToday += canUse;
                    dailyScheduledHours[dayKey] += canUse;
                }
            }

            if (slots.Any())
            {
                var status = remainingHours > 0.5 ? "Partial" : "Scheduled";

                // Update status on all events for this task
                foreach (var te in newTaskEvents.Where(e => e.TaskId == task.TaskId))
                    te.Status = status;

                result.ScheduledTasks.Add(new ScheduledTaskDto
                {
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Slots = slots
                });

                if (remainingHours > 0.5)
                {
                    result.UnscheduledTasks.Add(new UnscheduledTaskDto
                    {
                        TaskId = task.TaskId,
                        Title = task.Title,
                        Reason = $"Only {totalHours - remainingHours:F1}h of {totalHours:F1}h could be scheduled"
                    });
                }
            }
            else
            {
                result.UnscheduledTasks.Add(new UnscheduledTaskDto
                {
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Reason = "No available time slots before due date"
                });
            }
        }

        // 6. Save all new task events
        if (newTaskEvents.Any())
        {
            _db.TaskEvents.AddRange(newTaskEvents);
            await _db.SaveChangesAsync();
        }

        // 7. Build daily workload summary
        var allDays = Enumerable.Range(0, (scheduleEnd - scheduleStart).Days)
            .Select(i => scheduleStart.AddDays(i))
            .ToList();

        foreach (var day in allDays)
        {
            var dayKey = day.Date;
            var scheduledHours = dailyScheduledHours.GetValueOrDefault(dayKey, 0);
            var availableHours = (dayEnd - dayStart) - GetBlockedHours(day, fixedEvents, dayStart, dayEnd);
            var isOverloaded = scheduledHours > maxDaily;

            result.DailyWorkload.Add(new DailyWorkloadDto
            {
                Date = dayKey,
                ScheduledHours = Math.Round(scheduledHours, 1),
                AvailableHours = Math.Round(Math.Max(0, availableHours), 1),
                IsOverloaded = isOverloaded
            });

            if (isOverloaded)
                result.OverloadedDays.Add(dayKey.ToString("yyyy-MM-dd"));
        }

        result.ScheduledCount = result.ScheduledTasks.Count;
        result.UnscheduledCount = result.UnscheduledTasks.Count;

        return result;
    }

    public async Task<SchedulingStatusDto> GetSchedulingStatusAsync(string email)
    {
        var now = DateTime.Now;

        // Load user preferences
        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDaily = prefs?.MaxDailyStudyHours ?? 6.0;

        var tasks = await _db.Tasks
            .Include(t => t.TaskEvents)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate.HasValue)
            .ToListAsync();

        var scheduledCount = tasks.Count(t => t.TaskEvents.Any(te => te.Status == "Scheduled" || te.Status == "Partial"));
        var unscheduledCount = tasks.Count(t => !t.TaskEvents.Any());

        // Build workload for next 14 days
        var scheduleStart = now.Date;
        var scheduleEnd = now.Date.AddDays(14);

        var taskEvents = await _db.TaskEvents
            .Where(te => te.StudentTask.Email == email
                && te.From >= scheduleStart && te.From < scheduleEnd
                && te.Status == "Scheduled")
            .ToListAsync();

        var fixedEvents = await GetExpandedEvents(email, scheduleStart, scheduleEnd);

        var result = new SchedulingStatusDto
        {
            ScheduledCount = scheduledCount,
            UnscheduledCount = unscheduledCount
        };

        for (var day = scheduleStart; day < scheduleEnd; day = day.AddDays(1))
        {
            // Sum ALL event hours for this day (classes, work, personal, tasks)
            var dayFixedHours = fixedEvents
                .Where(e => e.From.Date == day.Date)
                .Sum(e => (e.To - e.From).TotalHours);
            var dayTaskHours = taskEvents
                .Where(te => te.From.Date == day.Date)
                .Sum(te => (te.To - te.From).TotalHours);
            var scheduledHours = dayFixedHours + dayTaskHours;
            var availableHours = (dayEnd - dayStart) - GetBlockedHours(day, fixedEvents, dayStart, dayEnd);
            var isOverloaded = scheduledHours > maxDaily;

            result.DailyWorkload.Add(new DailyWorkloadDto
            {
                Date = day,
                ScheduledHours = Math.Round(scheduledHours, 1),
                AvailableHours = Math.Round(Math.Max(0, availableHours), 1),
                IsOverloaded = isOverloaded
            });

            if (isOverloaded)
                result.OverloadedDays.Add(day.ToString("yyyy-MM-dd"));
        }

        return result;
    }

    /// <summary>
    /// Returns free time slots for a given day (between dayStart and dayEnd),
    /// excluding existing fixed events and already-scheduled task events.
    /// </summary>
    private List<(DateTime From, DateTime To)> GetFreeSlots(
        DateTime day, List<Event> fixedEvents, List<TaskEvent> newTaskEvents,
        int dayStartHour, int dayEndHour)
    {
        var dayStart = day.Date.AddHours(dayStartHour);
        var dayEnd = day.Date.AddHours(dayEndHour);

        // If the day is today, start from now (rounded up to next 30 min)
        if (day.Date == DateTime.Now.Date)
        {
            var now = DateTime.Now;
            var roundedNow = new DateTime(now.Year, now.Month, now.Day,
                now.Hour, now.Minute >= 30 ? 30 : 0, 0).AddMinutes(30);
            if (roundedNow > dayStart)
                dayStart = roundedNow;
        }

        if (dayStart >= dayEnd) return new List<(DateTime, DateTime)>();

        // Collect all busy intervals for this day
        var busy = new List<(DateTime From, DateTime To)>();

        foreach (var e in fixedEvents)
        {
            if (e.From.Date == day.Date || (e.From < dayEnd && e.To > dayStart))
            {
                var from = e.From < dayStart ? dayStart : e.From;
                var to = e.To > dayEnd ? dayEnd : e.To;
                if (from < to) busy.Add((from, to));
            }
        }

        foreach (var te in newTaskEvents)
        {
            if (te.From.Date == day.Date)
            {
                var from = te.From < dayStart ? dayStart : te.From;
                var to = te.To > dayEnd ? dayEnd : te.To;
                if (from < to) busy.Add((from, to));
            }
        }

        // Sort and merge busy intervals
        busy = busy.OrderBy(b => b.From).ToList();
        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var b in busy)
        {
            if (merged.Any() && b.From <= merged.Last().To)
            {
                var last = merged.Last();
                merged[merged.Count - 1] = (last.From, b.To > last.To ? b.To : last.To);
            }
            else
            {
                merged.Add(b);
            }
        }

        // Extract free slots between busy intervals
        var free = new List<(DateTime From, DateTime To)>();
        var cursor = dayStart;
        foreach (var b in merged)
        {
            if (cursor < b.From)
                free.Add((cursor, b.From));
            cursor = b.To > cursor ? b.To : cursor;
        }
        if (cursor < dayEnd)
            free.Add((cursor, dayEnd));

        return free;
    }

    /// <summary>
    /// Returns total blocked (busy) hours for a day from fixed events.
    /// </summary>
    private double GetBlockedHours(DateTime day, List<Event> fixedEvents, int dayStartHour, int dayEndHour)
    {
        var dayStart = day.Date.AddHours(dayStartHour);
        var dayEnd = day.Date.AddHours(dayEndHour);

        return fixedEvents
            .Where(e => e.From < dayEnd && e.To > dayStart)
            .Sum(e =>
            {
                var from = e.From < dayStart ? dayStart : e.From;
                var to = e.To > dayEnd ? dayEnd : e.To;
                return Math.Max(0, (to - from).TotalHours);
            });
    }

    /// <summary>
    /// Fetches events for a user and expands recurring events into weekly copies
    /// so the scheduler never places tasks on top of recurring classes/work.
    /// </summary>
    private async Task<List<Event>> GetExpandedEvents(
        string email, DateTime rangeStart, DateTime rangeEnd)
    {
        var events = await _db.Events
            .Where(e => e.Email == email &&
                ((e.From < rangeEnd && e.To > rangeStart) || e.Recurring))
            .OrderBy(e => e.From)
            .ToListAsync();

        var expanded = new List<Event>();
        foreach (var evt in events)
        {
            if (evt.From < rangeEnd && evt.To > rangeStart)
                expanded.Add(evt);

            if (evt.Recurring)
            {
                var duration = evt.To - evt.From;
                var nextFrom = evt.From.AddDays(7);
                while (nextFrom < rangeEnd)
                {
                    var nextTo = nextFrom.Add(duration);
                    if (nextTo > rangeStart)
                    {
                        expanded.Add(new Event
                        {
                            Email = email,
                            From = nextFrom,
                            To = nextTo,
                            Recurring = true
                        });
                    }
                    nextFrom = nextFrom.AddDays(7);
                }
            }
        }

        return expanded;
    }
}
