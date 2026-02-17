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
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;
        int maxContinuousMinutes = prefs?.MaxContinuousMinutes ?? 90;
        int breakMinutes = prefs?.BreakDurationMinutes ?? 15;
        double examPrepHoursPerDay = prefs?.ExamPrepHoursPerDay ?? 5.0;
        int examPrepDays = prefs?.ExamPrepDays ?? 3;

        // 1. Get all incomplete tasks with due dates (only leaf tasks - no parents with children)
        var allTasks = await _db.Tasks
            .Include(t => t.Course)
            .Include(t => t.TaskEvents)
            .Include(t => t.SubTasks)
            .Include(t => t.SharedTask)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate.HasValue)
            .ToListAsync();
        var tasks = allTasks.Where(t => !t.SubTasks.Any()).ToList();

        // 2. Determine scheduling window
        var maxDueDate = tasks.Any() ? tasks.Max(t => t.DueDate!.Value) : now.AddDays(14);
        if (maxDueDate < now.AddDays(7)) maxDueDate = now.AddDays(7);
        var scheduleEnd = maxDueDate.Date.AddDays(1);
        var scheduleStart = now.Date;

        // 3. Clear existing auto-scheduled TaskEvents
        var existingTaskEventIds = await _db.TaskEvents
            .Where(te => te.StudentTask.Email == email && (te.Status == "Scheduled" || te.Status == "Partial"))
            .Select(te => te.EventId)
            .ToListAsync();

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

        // Also load typed events for relocation suggestions and workload breakdown
        var typedEvents = await _db.Events
            .Where(e => e.Email == email && ((e.From < scheduleEnd && e.To > scheduleStart) || e.Recurring))
            .ToListAsync();
        var classEventIds = await _db.ClassEvents.Select(ce => ce.EventId).ToListAsync();
        var workEvents = await _db.WorkEvents.Where(w => w.Email == email).ToListAsync();
        var workEventIds = workEvents.Select(w => w.EventId).ToList();
        var personalEvents = await _db.PersonalEvents.Where(p => p.Email == email).ToListAsync();
        var personalEventIds = personalEvents.Select(p => p.EventId).ToList();
        var workEventLookup = workEvents.ToDictionary(w => w.EventId);
        var personalEventLookup = personalEvents.ToDictionary(p => p.EventId);

        // Add lunch break as blocked slots if configured
        if (prefs?.LunchBreakStart != null && prefs?.LunchBreakEnd != null)
        {
            for (var day = scheduleStart; day < scheduleEnd; day = day.AddDays(1))
            {
                fixedEvents.Add(new Event
                {
                    Email = email,
                    From = day.Add(prefs.LunchBreakStart.Value),
                    To = day.Add(prefs.LunchBreakEnd.Value),
                    Recurring = false
                });
            }
        }

        // Load exams to block exam days and reserve pre-exam study time
        var exams = await _db.Exams
            .Include(e => e.Course)
            .Where(e => e.Course.UserCourses.Any(uc => uc.Email == email)
                && e.Date >= scheduleStart && e.Date <= scheduleEnd)
            .ToListAsync();

        var examDays = new HashSet<DateTime>(exams.Select(e => e.Date.Date));

        // Auto-create "Study for exam" tasks with configurable prep hours/days
        var examStudyTasks = new List<StudentTask>();
        foreach (var exam in exams)
        {
            if (exam.Date.Date <= now.Date) continue;

            // Get per-course or global prep settings
            var coursePrepHoursPerDay = exam.Course?.ExamPrepHoursPerDay ?? examPrepHoursPerDay;
            var coursePrepDays = exam.Course?.ExamPrepDays ?? examPrepDays;
            var totalPrepHours = coursePrepHoursPerDay * coursePrepDays;

            var existingStudyTask = await _db.Tasks
                .FirstOrDefaultAsync(t => t.Email == email
                    && t.CourseId == exam.CourseId
                    && t.Type == "Study for exam"
                    && t.DueDate == exam.Date.Date
                    && !t.IsCompleted);

            if (existingStudyTask == null)
            {
                var courseName = exam.Course?.CourseName ?? (await _db.Courses.FindAsync(exam.CourseId))?.CourseName ?? "Exam";
                existingStudyTask = new StudentTask
                {
                    Email = email,
                    CourseId = exam.CourseId,
                    Title = $"Study for exam - {courseName}",
                    Type = "Study for exam",
                    EstimatedHours = (decimal)totalPrepHours,
                    DueDate = exam.Date.Date,
                    IsCompleted = false,
                    Priority = "High"
                };
                _db.Tasks.Add(existingStudyTask);
                await _db.SaveChangesAsync();
            }
            else if ((double)(existingStudyTask.EstimatedHours ?? 0) != totalPrepHours)
            {
                // Update hours if prep settings changed
                existingStudyTask.EstimatedHours = (decimal)totalPrepHours;
                await _db.SaveChangesAsync();
            }

            examStudyTasks.Add(existingStudyTask);
        }

        // Map exam dates to their study tasks for targeted scheduling
        var examStudyByDate = new Dictionary<DateTime, List<(StudentTask Task, double TargetHours)>>();
        foreach (var exam in exams)
        {
            if (exam.Date.Date <= now.Date) continue;
            var studyTask = examStudyTasks.FirstOrDefault(t => t.CourseId == exam.CourseId && t.DueDate == exam.Date.Date);
            if (studyTask == null) continue;

            var coursePrepHoursPerDay = exam.Course?.ExamPrepHoursPerDay ?? examPrepHoursPerDay;
            var coursePrepDays = exam.Course?.ExamPrepDays ?? examPrepDays;

            for (int d = 1; d <= coursePrepDays; d++)
            {
                var prepDay = exam.Date.Date.AddDays(-d);
                if (prepDay >= scheduleStart)
                {
                    if (!examStudyByDate.ContainsKey(prepDay))
                        examStudyByDate[prepDay] = new List<(StudentTask, double)>();
                    if (!examStudyByDate[prepDay].Any(x => x.Task.TaskId == studyTask.TaskId))
                        examStudyByDate[prepDay].Add((studyTask, coursePrepHoursPerDay));
                }
            }
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

        // 4. Compute priority score for every task (new formula — computed, not user-defined)
        var scoredTasks = tasks.Select(t =>
        {
            var daysUntilDue = Math.Max(0.1, (t.DueDate!.Value - now).TotalDays);
            var hours = GetEffectiveHours(t, courseRatios, prefs);
            var credits = (double)(t.Course?.Credits ?? 3);
            var isShared = t.SharedTask != null;

            var score = (1.0 / daysUntilDue) * 50
                      + Math.Min(hours, 10) * 5
                      + credits * 4
                      + (isShared ? 25 : 0);

            var priority = score switch
            {
                > 70 => "High",
                > 35 => "Medium",
                _ => "Low"
            };

            // Store computed priority on the task entity
            t.Priority = priority;

            return new { Task = t, Score = score, Priority = priority, EffectiveHours = hours };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // Save computed priorities
        await _db.SaveChangesAsync();

        // Track scheduled hours per day
        var dailyScheduledStudyHours = new Dictionary<DateTime, double>();
        var newTaskEvents = new List<TaskEvent>();

        // Helper: get total event hours for a day (classes + work + personal, NOT study tasks)
        double GetTotalFixedEventHours(DateTime day)
        {
            return fixedEvents
                .Where(e => e.From.Date == day.Date || (e.From < day.Date.AddDays(1) && e.To > day.Date))
                .Sum(e =>
                {
                    var from = e.From < day.Date.AddHours(dayStart) ? day.Date.AddHours(dayStart) : e.From;
                    var to = e.To > day.Date.AddHours(dayEnd) ? day.Date.AddHours(dayEnd) : e.To;
                    return Math.Max(0, (to - from).TotalHours);
                });
        }

        // 5a. Schedule exam study tasks first on their designated prep days
        var examStudyRemainingHours = examStudyTasks.ToDictionary(t => t.TaskId, t => (double)(t.EstimatedHours ?? 15));
        foreach (var (prepDay, studyEntries) in examStudyByDate.OrderBy(kv => kv.Key))
        {
            if (examDays.Contains(prepDay)) continue;
            var dayKey = prepDay.Date;
            if (!dailyScheduledStudyHours.ContainsKey(dayKey))
                dailyScheduledStudyHours[dayKey] = 0;

            foreach (var (studyTask, targetHours) in studyEntries)
            {
                if (examStudyRemainingHours[studyTask.TaskId] <= 0) continue;

                var dayFreeSlots = GetFreeSlots(prepDay, fixedEvents, newTaskEvents, dayStart, dayEnd);
                var studyToday = 0.0;

                foreach (var freeSlot in dayFreeSlots)
                {
                    if (studyToday >= targetHours) break;
                    if (examStudyRemainingHours[studyTask.TaskId] <= 0) break;

                    var slotDuration = (freeSlot.To - freeSlot.From).TotalHours;
                    var canUse = Math.Min(slotDuration,
                        Math.Min(examStudyRemainingHours[studyTask.TaskId],
                        targetHours - studyToday));

                    if (canUse < 0.5) continue;
                    canUse = Math.Floor(canUse * 2) / 2;
                    if (canUse <= 0) continue;

                    var slotFrom = freeSlot.From;
                    var slotTo = slotFrom.AddHours(canUse);

                    newTaskEvents.Add(new TaskEvent
                    {
                        Email = email,
                        From = slotFrom,
                        To = slotTo,
                        Recurring = false,
                        TaskId = studyTask.TaskId,
                        Priority = "High",
                        Status = "Scheduled"
                    });

                    studyToday += canUse;
                    examStudyRemainingHours[studyTask.TaskId] -= canUse;
                    dailyScheduledStudyHours[dayKey] = dailyScheduledStudyHours.GetValueOrDefault(dayKey, 0) + canUse;
                }

                var existingScheduled = result.ScheduledTasks.FirstOrDefault(st => st.TaskId == studyTask.TaskId);
                if (existingScheduled == null)
                {
                    existingScheduled = new ScheduledTaskDto { TaskId = studyTask.TaskId, Title = studyTask.Title, Slots = new List<ScheduledSlotDto>() };
                    result.ScheduledTasks.Add(existingScheduled);
                }
                existingScheduled.Slots.AddRange(
                    newTaskEvents.Where(te => te.TaskId == studyTask.TaskId && te.From.Date == dayKey)
                        .Select(te => new ScheduledSlotDto { From = te.From, To = te.To }));
            }
        }

        // 5b. Schedule regular tasks in priority order
        var examStudyTaskIds = new HashSet<int>(examStudyTasks.Select(t => t.TaskId));
        foreach (var scored in scoredTasks)
        {
            var task = scored.Task;
            if (examStudyTaskIds.Contains(task.TaskId)) continue;

            var totalHours = scored.EffectiveHours;
            var remainingHours = totalHours;
            var slots = new List<ScheduledSlotDto>();
            var dueDate = task.DueDate!.Value < now ? now.Date.AddDays(7) : task.DueDate!.Value.Date;

            if (!task.AllowSplitting)
            {
                // NON-SPLITTING: find a single continuous window that fits the entire task
                var totalNeeded = totalHours + (Math.Floor(totalHours / (maxContinuousMinutes / 60.0)) * (breakMinutes / 60.0));

                bool scheduled = false;
                for (var day = scheduleStart; day < dueDate && !scheduled; day = day.AddDays(1))
                {
                    var dayKey = day.Date;
                    if (examDays.Contains(dayKey)) continue;

                    if (!dailyScheduledStudyHours.ContainsKey(dayKey))
                        dailyScheduledStudyHours[dayKey] = 0;

                    // Check both study and total daily limits
                    var studyAvailable = maxDailyStudy - dailyScheduledStudyHours[dayKey];
                    if (studyAvailable < totalHours) continue;

                    var fixedHours = GetTotalFixedEventHours(day);
                    var totalDayHours = fixedHours + dailyScheduledStudyHours[dayKey] + totalHours;
                    if (totalDayHours > maxDailyTotal) continue;

                    var dayFreeSlots = GetFreeSlots(day, fixedEvents, newTaskEvents, dayStart, dayEnd);

                    foreach (var freeSlot in dayFreeSlots)
                    {
                        var slotDuration = (freeSlot.To - freeSlot.From).TotalHours;
                        if (slotDuration >= totalNeeded)
                        {
                            // Schedule with internal breaks
                            var sessions = SplitIntoSessionsWithBreaks(freeSlot.From, totalHours, maxContinuousMinutes, breakMinutes);
                            foreach (var session in sessions)
                            {
                                newTaskEvents.Add(new TaskEvent
                                {
                                    Email = email,
                                    From = session.From,
                                    To = session.To,
                                    Recurring = false,
                                    TaskId = task.TaskId,
                                    Priority = scored.Priority,
                                    Status = "Scheduled"
                                });
                                slots.Add(new ScheduledSlotDto { From = session.From, To = session.To });
                            }
                            dailyScheduledStudyHours[dayKey] += totalHours;
                            remainingHours = 0;
                            scheduled = true;
                            break;
                        }
                    }
                }

                if (!scheduled)
                {
                    result.UnscheduledTasks.Add(new UnscheduledTaskDto
                    {
                        TaskId = task.TaskId,
                        Title = task.Title,
                        Reason = "No continuous slot available"
                    });

                    // Generate relocation suggestions for work/personal events
                    GenerateRelocationSuggestions(result, task, scheduleStart, dueDate, fixedEvents,
                        newTaskEvents, dayStart, dayEnd, totalNeeded, examDays, typedEvents, workEventIds, personalEventIds,
                        workEventLookup, personalEventLookup);
                }
            }
            else
            {
                // SPLITTING MODE: spread across available slots (original behavior)
                for (var day = scheduleStart; day < dueDate && remainingHours > 0; day = day.AddDays(1))
                {
                    var dayKey = day.Date;
                    if (examDays.Contains(dayKey)) continue;

                    if (!dailyScheduledStudyHours.ContainsKey(dayKey))
                        dailyScheduledStudyHours[dayKey] = 0;

                    var studyAvailable = maxDailyStudy - dailyScheduledStudyHours[dayKey];
                    if (studyAvailable <= 0) continue;

                    // Check total daily load
                    var fixedHours = GetTotalFixedEventHours(day);
                    var totalDayHours = fixedHours + dailyScheduledStudyHours[dayKey];
                    if (totalDayHours >= maxDailyTotal) continue;

                    var maxCanAddTotal = maxDailyTotal - totalDayHours;

                    var dayFreeSlots = GetFreeSlots(day, fixedEvents, newTaskEvents, dayStart, dayEnd);
                    var maxContinuousHours = maxContinuousMinutes / 60.0;

                    var taskHoursToday = 0.0;
                    foreach (var freeSlot in dayFreeSlots)
                    {
                        if (remainingHours <= 0) break;
                        if (studyAvailable - taskHoursToday <= 0) break;
                        if (taskHoursToday >= maxContinuousHours) break;

                        var slotDuration = (freeSlot.To - freeSlot.From).TotalHours;
                        var canUse = Math.Min(slotDuration,
                            Math.Min(remainingHours,
                            Math.Min(studyAvailable - taskHoursToday,
                            Math.Min(maxContinuousHours - taskHoursToday,
                            maxCanAddTotal - taskHoursToday))));

                        if (canUse < 0.5) continue;
                        canUse = Math.Floor(canUse * 2) / 2;
                        if (canUse <= 0) continue;

                        var slotFrom = freeSlot.From;
                        var slotTo = slotFrom.AddHours(canUse);

                        newTaskEvents.Add(new TaskEvent
                        {
                            Email = email,
                            From = slotFrom,
                            To = slotTo,
                            Recurring = false,
                            TaskId = task.TaskId,
                            Priority = scored.Priority,
                            Status = "Scheduled"
                        });

                        slots.Add(new ScheduledSlotDto { From = slotFrom, To = slotTo });
                        remainingHours -= canUse;
                        taskHoursToday += canUse;
                        dailyScheduledStudyHours[dayKey] += canUse;
                    }
                }
            }

            if (slots.Any())
            {
                var status = remainingHours > 0.5 ? "Partial" : "Scheduled";
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
            else if (task.AllowSplitting)
            {
                result.UnscheduledTasks.Add(new UnscheduledTaskDto
                {
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Reason = "No available time slots before due date"
                });

                GenerateRelocationSuggestions(result, task, scheduleStart, dueDate, fixedEvents,
                    newTaskEvents, dayStart, dayEnd, totalHours, examDays, typedEvents, workEventIds, personalEventIds,
                    workEventLookup, personalEventLookup);
            }
        }

        // 6. Save all new task events
        if (newTaskEvents.Any())
        {
            _db.TaskEvents.AddRange(newTaskEvents);
            await _db.SaveChangesAsync();
        }

        // 7. Build daily workload summary with breakdown
        var allDays = Enumerable.Range(0, (scheduleEnd - scheduleStart).Days)
            .Select(i => scheduleStart.AddDays(i))
            .ToList();

        foreach (var day in allDays)
        {
            var dayKey = day.Date;
            var studyHours = dailyScheduledStudyHours.GetValueOrDefault(dayKey, 0);
            var availableHours = (dayEnd - dayStart) - GetBlockedHours(day, fixedEvents, dayStart, dayEnd);

            // Breakdown by event type for this day
            var dayExpandedEvents = fixedEvents.Where(e => e.From.Date == dayKey || (e.From < dayKey.AddDays(1) && e.To > dayKey)).ToList();
            double classHours = 0, workHours = 0, personalHours = 0;
            foreach (var evt in dayExpandedEvents)
            {
                var duration = Math.Max(0, (evt.To - evt.From).TotalHours);
                if (classEventIds.Contains(evt.EventId))
                    classHours += duration;
                else if (workEventIds.Contains(evt.EventId))
                    workHours += duration;
                else if (personalEventIds.Contains(evt.EventId))
                    personalHours += duration;
            }

            var totalHoursDay = studyHours + workHours + classHours + personalHours;
            var studyLoad = maxDailyStudy > 0 ? (studyHours / maxDailyStudy) * 100 : 0;
            var totalLoad = maxDailyTotal > 0 ? (totalHoursDay / maxDailyTotal) * 100 : 0;
            var isOverloaded = Math.Max(studyLoad, totalLoad) > 100;

            result.DailyWorkload.Add(new DailyWorkloadDto
            {
                Date = dayKey,
                ScheduledHours = Math.Round(studyHours, 1),
                AvailableHours = Math.Round(Math.Max(0, availableHours), 1),
                IsOverloaded = isOverloaded,
                StudyHours = Math.Round(studyHours, 1),
                WorkHours = Math.Round(workHours, 1),
                ClassHours = Math.Round(classHours, 1),
                PersonalHours = Math.Round(personalHours, 1),
                TotalHours = Math.Round(totalHoursDay, 1)
            });

            if (isOverloaded)
                result.OverloadedDays.Add(dayKey.ToString("yyyy-MM-dd"));
        }

        result.ScheduledCount = result.ScheduledTasks.Count;
        result.UnscheduledCount = result.UnscheduledTasks.Count;

        return result;
    }

    /// <summary>
    /// Gets effective hours for a task using the hierarchy:
    /// ML ratio (≥2 completed tasks) → Course default → Global default → 4.0
    /// </summary>
    private double GetEffectiveHours(StudentTask task, Dictionary<int, double> courseRatios, SchedulingPreferences? prefs)
    {
        double baseHours;

        if (task.EstimatedHours.HasValue && task.EstimatedHours > 0)
        {
            baseHours = (double)task.EstimatedHours;
        }
        else
        {
            baseHours = task.Course?.DefaultTaskEstimatedHours
                        ?? prefs?.DefaultTaskEstimatedHours
                        ?? 4.0;
        }

        if (courseRatios.TryGetValue(task.CourseId, out var ratio))
            baseHours *= ratio;

        return baseHours;
    }

    /// <summary>
    /// Splits a continuous block into sessions with breaks.
    /// E.g., 3h task with 90-min max continuous + 15-min break:
    /// 09:00-10:30 (90 min), break, 10:45-12:15 (90 min)
    /// </summary>
    private List<(DateTime From, DateTime To)> SplitIntoSessionsWithBreaks(
        DateTime start, double totalHours, int maxContinuousMinutes, int breakMinutes)
    {
        var sessions = new List<(DateTime From, DateTime To)>();
        var remainingMinutes = totalHours * 60;
        var cursor = start;

        while (remainingMinutes > 0)
        {
            var sessionMinutes = Math.Min(remainingMinutes, maxContinuousMinutes);
            sessions.Add((cursor, cursor.AddMinutes(sessionMinutes)));
            remainingMinutes -= sessionMinutes;
            cursor = cursor.AddMinutes(sessionMinutes);

            if (remainingMinutes > 0)
            {
                cursor = cursor.AddMinutes(breakMinutes); // break gap (not an event)
            }
        }

        return sessions;
    }

    /// <summary>
    /// Generates relocation suggestions for work/personal events that block an unscheduled task.
    /// </summary>
    private void GenerateRelocationSuggestions(
        SchedulingResultDto result, StudentTask task,
        DateTime scheduleStart, DateTime dueDate,
        List<Event> fixedEvents, List<TaskEvent> newTaskEvents,
        int dayStart, int dayEnd, double neededHours,
        HashSet<DateTime> examDays,
        List<Event> typedEvents, List<int> workEventIds, List<int> personalEventIds,
        Dictionary<int, WorkEvent> workEventLookup, Dictionary<int, PersonalEvent> personalEventLookup)
    {
        for (var day = scheduleStart; day < dueDate; day = day.AddDays(1))
        {
            if (examDays.Contains(day.Date)) continue;

            // Find work/personal events on this day that could be moved
            var movableEvents = typedEvents
                .Where(e => (e.From.Date == day.Date || (e.From < day.Date.AddDays(1) && e.To > day.Date))
                    && (workEventIds.Contains(e.EventId) || personalEventIds.Contains(e.EventId)))
                .ToList();

            foreach (var evt in movableEvents)
            {
                var evtDuration = (evt.To - evt.From).TotalHours;
                if (evtDuration >= neededHours * 0.5) // Only suggest if the event frees meaningful time
                {
                    var eventType = workEventIds.Contains(evt.EventId) ? "Work" : "Personal";
                    var eventTitle = GetEventTitle(evt.EventId, eventType, workEventLookup, personalEventLookup);
                    result.RelocationSuggestions.Add(new RelocationSuggestionDto
                    {
                        EventId = evt.EventId,
                        EventTitle = eventTitle,
                        EventType = eventType,
                        CurrentFrom = evt.From,
                        CurrentTo = evt.To,
                        BlockedTaskTitle = task.Title,
                        Message = $"Moving \"{eventTitle}\" from {evt.From:ddd HH:mm}-{evt.To:HH:mm} would free up space for \"{task.Title}\""
                    });
                }
            }

            if (result.RelocationSuggestions.Any(rs => rs.BlockedTaskTitle == task.Title))
                break; // One suggestion per task is enough
        }
    }

    private static string GetEventTitle(int eventId, string eventType,
        Dictionary<int, WorkEvent> workLookup, Dictionary<int, PersonalEvent> personalLookup)
    {
        if (eventType == "Work" && workLookup.TryGetValue(eventId, out var work))
            return !string.IsNullOrWhiteSpace(work.WorkPlace) ? work.WorkPlace : "Work";
        if (eventType == "Personal" && personalLookup.TryGetValue(eventId, out var personal))
            return !string.IsNullOrWhiteSpace(personal.Description) ? personal.Description
                : !string.IsNullOrWhiteSpace(personal.Type) ? personal.Type : "Personal";
        return eventType;
    }

    public async Task<SchedulingStatusDto> GetSchedulingStatusAsync(string email)
    {
        var now = DateTime.Now;

        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        var tasks = await _db.Tasks
            .Include(t => t.TaskEvents)
            .Where(t => t.Email == email && !t.IsCompleted && t.DueDate.HasValue)
            .ToListAsync();

        var scheduledCount = tasks.Count(t => t.TaskEvents.Any(te => te.Status == "Scheduled" || te.Status == "Partial"));
        var unscheduledCount = tasks.Count(t => !t.TaskEvents.Any());

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

        // Load typed event IDs for breakdown and relocation suggestions
        var workEvents = await _db.WorkEvents
            .Where(w => w.Email == email)
            .ToListAsync();
        var workEventIds = workEvents.Select(w => w.EventId).ToList();
        var workEventLookup = workEvents.ToDictionary(w => w.EventId);
        var personalEvents = await _db.PersonalEvents
            .Where(p => p.Email == email)
            .ToListAsync();
        var personalEventIds = personalEvents.Select(p => p.EventId).ToList();
        var personalEventLookup = personalEvents.ToDictionary(p => p.EventId);
        var classEventIds = await _db.ClassEvents
            .Where(c => c.Email == email)
            .Select(c => c.EventId)
            .ToListAsync();

        for (var day = scheduleStart; day < scheduleEnd; day = day.AddDays(1))
        {
            var dayEvents = fixedEvents.Where(e => e.From.Date == day.Date).ToList();
            double classHours = 0, workHours = 0, personalHours = 0;
            foreach (var evt in dayEvents)
            {
                var duration = Math.Max(0, (evt.To - evt.From).TotalHours);
                if (classEventIds.Contains(evt.EventId))
                    classHours += duration;
                else if (workEventIds.Contains(evt.EventId))
                    workHours += duration;
                else if (personalEventIds.Contains(evt.EventId))
                    personalHours += duration;
            }

            var dayTaskHours = taskEvents
                .Where(te => te.From.Date == day.Date)
                .Sum(te => (te.To - te.From).TotalHours);
            var totalHours = dayTaskHours + classHours + workHours + personalHours;
            var availableHours = (dayEnd - dayStart) - GetBlockedHours(day, fixedEvents, dayStart, dayEnd);

            var studyLoad = maxDailyStudy > 0 ? (dayTaskHours / maxDailyStudy) * 100 : 0;
            var totalLoad = maxDailyTotal > 0 ? (totalHours / maxDailyTotal) * 100 : 0;
            var isOverloaded = Math.Max(studyLoad, totalLoad) > 100;

            result.DailyWorkload.Add(new DailyWorkloadDto
            {
                Date = day,
                ScheduledHours = Math.Round(totalHours, 1),
                AvailableHours = Math.Round(Math.Max(0, availableHours), 1),
                IsOverloaded = isOverloaded,
                StudyHours = Math.Round(dayTaskHours, 1),
                WorkHours = Math.Round(workHours, 1),
                ClassHours = Math.Round(classHours, 1),
                PersonalHours = Math.Round(personalHours, 1),
                TotalHours = Math.Round(totalHours, 1)
            });

            if (isOverloaded)
                result.OverloadedDays.Add(day.ToString("yyyy-MM-dd"));
        }

        // Generate relocation suggestions for unscheduled tasks
        var unscheduledTasks = tasks.Where(t => !t.TaskEvents.Any()).ToList();
        foreach (var task in unscheduledTasks)
        {
            var dueDate = task.DueDate!.Value;
            for (var day = scheduleStart; day < dueDate && day < scheduleEnd; day = day.AddDays(1))
            {
                var movableEvents = fixedEvents
                    .Where(e => e.From.Date == day.Date
                        && (workEventIds.Contains(e.EventId) || personalEventIds.Contains(e.EventId)))
                    .ToList();

                foreach (var evt in movableEvents)
                {
                    var evtDuration = (evt.To - evt.From).TotalHours;
                    if (evtDuration >= 1.0)
                    {
                        var eventType = workEventIds.Contains(evt.EventId) ? "Work" : "Personal";
                        var eventTitle = GetEventTitle(evt.EventId, eventType, workEventLookup, personalEventLookup);
                        result.RelocationSuggestions.Add(new RelocationSuggestionDto
                        {
                            EventId = evt.EventId,
                            EventTitle = eventTitle,
                            EventType = eventType,
                            CurrentFrom = evt.From,
                            CurrentTo = evt.To,
                            BlockedTaskTitle = task.Title,
                            Message = $"Moving \"{eventTitle}\" from {evt.From:ddd HH:mm}-{evt.To:HH:mm} would free up space for \"{task.Title}\""
                        });
                        break;
                    }
                }

                if (result.RelocationSuggestions.Any(rs => rs.BlockedTaskTitle == task.Title))
                    break;
            }
        }

        return result;
    }

    private List<(DateTime From, DateTime To)> GetFreeSlots(
        DateTime day, List<Event> fixedEvents, List<TaskEvent> newTaskEvents,
        int dayStartHour, int dayEndHour)
    {
        var dayStart = day.Date.AddHours(dayStartHour);
        var dayEnd = day.Date.AddHours(dayEndHour);

        if (day.Date == DateTime.Now.Date)
        {
            var now = DateTime.Now;
            var roundedNow = new DateTime(now.Year, now.Month, now.Day,
                now.Hour, now.Minute >= 30 ? 30 : 0, 0).AddMinutes(30);
            if (roundedNow > dayStart)
                dayStart = roundedNow;
        }

        if (dayStart >= dayEnd) return new List<(DateTime, DateTime)>();

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
