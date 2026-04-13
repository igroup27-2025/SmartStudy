using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class SchedulingService
{
    private readonly DBservices _dal;

    private const int SlotMinutes = 30;

    public SchedulingService(DBservices dal)
    {
        _dal = dal;
    }

    public async Task<SchedulingResultDto> ScheduleAllTasksAsync(string email)
    {
        var now = DateTime.Now;
        var result = new SchedulingResultDto();

        // Load user preferences
        var prefs = await _dal.GetSchedPrefsByEmailAsync(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;
        int maxContinuousMinutes = prefs?.MaxContinuousMinutes ?? 90;
        int breakMinutes = prefs?.BreakDurationMinutes ?? 15;
        double examPrepHoursPerDay = prefs?.ExamPrepHoursPerDay ?? 5.0;
        int examPrepDays = prefs?.ExamPrepDays ?? 3;

        // 1. Get all incomplete leaf tasks with due dates
        var allLeafTasks = await _dal.GetIncompleteLeafTasksAsync(email);

        // 2. Determine scheduling window — must cover both task due dates AND
        // upcoming exam dates, otherwise exam-prep "Study for exam" tasks are
        // never created for exams that sit beyond the farthest task due date.
        var scheduleStart = now.Date;
        var maxDueDate = allLeafTasks.Any() ? allLeafTasks.Max(t => t.DueDate!.Value) : now.AddDays(14);

        // Peek at exams across a wide horizon so the window can be extended to
        // include them (and their prep days).
        var upcomingExams = await _dal.GetExamsForSchedulingAsync(email, scheduleStart, now.AddYears(1));
        if (upcomingExams.Any())
        {
            var maxExamDate = upcomingExams.Max(e => e.Date);
            if (maxExamDate > maxDueDate) maxDueDate = maxExamDate;
        }

        if (maxDueDate < now.AddDays(7)) maxDueDate = now.AddDays(7);
        var scheduleEnd = maxDueDate.Date.AddDays(1);

        // Load pinned task IDs
        var pinnedTaskIds = await _dal.GetPinnedTaskIdsAsync(email);
        var pinnedTaskIdSet = new HashSet<int>(pinnedTaskIds);

        // Load tasks with NeedReview events
        var needReviewTaskIds = await _dal.GetNeedReviewTaskIdsAsync(email);
        var needReviewTaskIdSet = new HashSet<int>(needReviewTaskIds);

        // 3. Clear existing auto-scheduled TaskEvents (SKIP pinned tasks)
        var existingTaskEvents = await _dal.GetTaskEventsByUserAndStatusAsync(email, "Scheduled", "Partial");

        var existingTaskEventIds = existingTaskEvents
            .Where(te => !pinnedTaskIdSet.Contains(te.TaskId))
            .Select(te => te.EventId)
            .ToList();

        if (existingTaskEventIds.Any())
        {
            foreach (var eventId in existingTaskEventIds)
                await _dal.DeleteEventByIdAsync(eventId);
        }

        // Add pinned TaskEvents as fixed busy intervals
        var pinnedTaskEventsList = existingTaskEvents
            .Where(te => pinnedTaskIdSet.Contains(te.TaskId))
            .ToList();

        // Reload events without the cleared task events (include recurring for expansion)
        var fixedEvents = await GetExpandedEvents(email, scheduleStart, scheduleEnd);

        // Include pinned TaskEvents as fixed (busy) intervals
        foreach (var pte in pinnedTaskEventsList)
        {
            fixedEvents.Add(new Event
            {
                EventId = pte.EventId,
                Email = email,
                From = pte.From,
                To = pte.To,
                Recurring = false
            });
        }

        // Also load typed events for relocation suggestions and workload breakdown
        var classEventIds = await _dal.GetClassEventIdsByUserAsync(email);
        var classEventIdSet = new HashSet<int>(classEventIds);
        var workEvents = await _dal.GetWorkEventsByUserAsync(email);
        var workEventIds = workEvents.Select(w => w.EventId).ToList();
        var workEventIdSet = new HashSet<int>(workEventIds);
        var personalEvents = await _dal.GetPersonalEventsByUserAsync(email);
        var personalEventIds = personalEvents.Select(p => p.EventId).ToList();
        var personalEventIdSet = new HashSet<int>(personalEventIds);
        var workEventLookup = workEvents.ToDictionary(w => w.EventId);
        var personalEventLookup = personalEvents.ToDictionary(p => p.EventId);

        // The typed events list for relocation suggestions
        var typedEvents = fixedEvents; // fixedEvents already has expanded events

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

        // Reuse the exams already fetched above (they span now → +1y).
        var exams = upcomingExams;
        var examDays = new HashSet<DateTime>(exams.Select(e => e.Date.Date));

        // Auto-create "Study for exam" tasks
        var examStudyTasks = new List<SimpleTaskRow>();
        foreach (var exam in exams)
        {
            if (exam.Date.Date <= now.Date) continue;

            var coursePrepHoursPerDay = exam.CourseExamPrepHoursPerDay ?? examPrepHoursPerDay;
            var coursePrepDays = exam.CourseExamPrepDays ?? examPrepDays;
            var totalPrepHours = coursePrepHoursPerDay * coursePrepDays;

            var existingStudyTask = await _dal.GetStudyForExamTaskAsync(email, exam.CourseId, exam.Date.Date);

            if (existingStudyTask == null)
            {
                var courseName = exam.CourseName;
                var taskId = await _dal.CreateTaskAsync(exam.CourseId, email,
                    $"Study for exam - {courseName}", "Study for exam",
                    (decimal)totalPrepHours, exam.Date.Date, null, true, "High", false);

                existingStudyTask = new SimpleTaskRow
                {
                    TaskId = taskId,
                    CourseId = exam.CourseId,
                    Title = $"Study for exam - {courseName}",
                    Type = "Study for exam",
                    EstimatedHours = (decimal)totalPrepHours,
                    DueDate = exam.Date.Date,
                    IsCompleted = false,
                    Priority = "High",
                    AllowSplitting = true,
                    Email = email
                };
            }
            else
            {
                if ((double)(existingStudyTask.EstimatedHours ?? 0) != totalPrepHours)
                {
                    await _dal.UpdateTaskAsync(existingStudyTask.TaskId, estimatedHours: (decimal)totalPrepHours);
                    existingStudyTask.EstimatedHours = (decimal)totalPrepHours;
                }
                if (!existingStudyTask.AllowSplitting)
                {
                    await _dal.UpdateTaskAsync(existingStudyTask.TaskId, allowSplitting: true);
                    existingStudyTask.AllowSplitting = true;
                }
            }

            examStudyTasks.Add(existingStudyTask);
        }

        // Clean up orphaned exam study tasks
        var validExamStudyIds = examStudyTasks.Select(t => t.TaskId).ToHashSet();
        var allStudyTaskIds = await _dal.GetOrphanedStudyTaskIdsAsync(email);
        var orphanedIds = allStudyTaskIds.Where(id => !validExamStudyIds.Contains(id)).ToList();

        foreach (var orphanId in orphanedIds)
        {
            await _dal.DeleteTaskWithEventsAsync(orphanId);
        }

        // Map exam dates to their study tasks for targeted scheduling
        var examStudyByDate = new Dictionary<DateTime, List<(SimpleTaskRow Task, double TargetHours)>>();
        foreach (var exam in exams)
        {
            if (exam.Date.Date <= now.Date) continue;
            var studyTask = examStudyTasks.FirstOrDefault(t => t.CourseId == exam.CourseId && t.DueDate == exam.Date.Date);
            if (studyTask == null) continue;

            var coursePrepHoursPerDay = exam.CourseExamPrepHoursPerDay ?? examPrepHoursPerDay;
            var coursePrepDays = exam.CourseExamPrepDays ?? examPrepDays;

            for (int d = 1; d <= coursePrepDays; d++)
            {
                var prepDay = exam.Date.Date.AddDays(-d);
                if (prepDay >= scheduleStart)
                {
                    if (!examStudyByDate.ContainsKey(prepDay))
                        examStudyByDate[prepDay] = new List<(SimpleTaskRow, double)>();
                    if (!examStudyByDate[prepDay].Any(x => x.Task.TaskId == studyTask.TaskId))
                        examStudyByDate[prepDay].Add((studyTask, coursePrepHoursPerDay));
                }
            }
        }

        // ML-adjusted hours
        var completedForML = await _dal.GetCompletedTasksForMLAsync(email);
        var courseRatios = completedForML
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.ActualHours / t.EstimatedHours));

        // 4. Compute priority score for every task
        var schedulableTasks = allLeafTasks
            .Where(t => !pinnedTaskIdSet.Contains(t.TaskId) && !needReviewTaskIdSet.Contains(t.TaskId))
            .ToList();
        var scoredTasks = schedulableTasks.Select(t =>
        {
            var daysUntilDue = Math.Max(0.1, (t.DueDate!.Value - now).TotalDays);
            var hours = GetEffectiveHours(t, courseRatios, prefs);
            var credits = (double)(t.CourseCredits ?? 3);
            var isShared = t.HasSharedTask;

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

            string finalPriority;
            if (!t.IsManualPriority)
            {
                finalPriority = priority;
                // Update priority in DB
                _ = _dal.UpdateTaskPriorityAsync(t.TaskId, priority);
            }
            else
            {
                finalPriority = t.Priority ?? priority;
            }

            return new { Task = t, Score = score, Priority = finalPriority, EffectiveHours = hours };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // Track scheduled hours per day
        var dailyScheduledStudyHours = new Dictionary<DateTime, double>();
        var newTaskEvents = new List<TaskEvent>();

        // Helper: get total event hours for a day
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
                var totalNeeded = totalHours + (Math.Floor(totalHours / (maxContinuousMinutes / 60.0)) * (breakMinutes / 60.0));

                bool scheduled = false;
                for (var day = scheduleStart; day < dueDate && !scheduled; day = day.AddDays(1))
                {
                    var dayKey = day.Date;
                    if (examDays.Contains(dayKey)) continue;

                    if (!dailyScheduledStudyHours.ContainsKey(dayKey))
                        dailyScheduledStudyHours[dayKey] = 0;

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

                    GenerateRelocationSuggestions(result, task.TaskId, task.Title, scheduleStart, dueDate, fixedEvents,
                        newTaskEvents, dayStart, dayEnd, totalNeeded, examDays, workEventIdSet, personalEventIdSet,
                        workEventLookup, personalEventLookup);
                }
            }
            else
            {
                for (var day = scheduleStart; day < dueDate && remainingHours > 0; day = day.AddDays(1))
                {
                    var dayKey = day.Date;
                    if (examDays.Contains(dayKey)) continue;

                    if (!dailyScheduledStudyHours.ContainsKey(dayKey))
                        dailyScheduledStudyHours[dayKey] = 0;

                    var studyAvailable = maxDailyStudy - dailyScheduledStudyHours[dayKey];
                    if (studyAvailable <= 0) continue;

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

                GenerateRelocationSuggestions(result, task.TaskId, task.Title, scheduleStart, dueDate, fixedEvents,
                    newTaskEvents, dayStart, dayEnd, totalHours, examDays, workEventIdSet, personalEventIdSet,
                    workEventLookup, personalEventLookup);
            }
        }

        // 6. Save all new task events
        foreach (var te in newTaskEvents)
        {
            await _dal.CreateTaskEventAsync(te.Email, te.From, te.To, te.Recurring, null,
                te.TaskId, te.Priority, te.Status);
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

            var dayExpandedEvents = fixedEvents.Where(e => e.From.Date == dayKey || (e.From < dayKey.AddDays(1) && e.To > dayKey)).ToList();
            double classHours = 0, workHours = 0, personalHours = 0;
            foreach (var evt in dayExpandedEvents)
            {
                var duration = Math.Max(0, (evt.To - evt.From).TotalHours);
                if (classEventIdSet.Contains(evt.EventId))
                    classHours += duration;
                else if (workEventIdSet.Contains(evt.EventId))
                    workHours += duration;
                else if (personalEventIdSet.Contains(evt.EventId))
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

    private double GetEffectiveHours(SchedulingTaskRow task, Dictionary<int, double> courseRatios, SchedulingPreferences? prefs)
    {
        double baseHours;

        if (task.EstimatedHours.HasValue && task.EstimatedHours > 0)
        {
            baseHours = (double)task.EstimatedHours;
        }
        else
        {
            baseHours = task.DefaultTaskEstimatedHours
                        ?? prefs?.DefaultTaskEstimatedHours
                        ?? 4.0;
        }

        if (courseRatios.TryGetValue(task.CourseId, out var ratio))
            baseHours *= ratio;

        return baseHours;
    }

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
                cursor = cursor.AddMinutes(breakMinutes);
            }
        }

        return sessions;
    }

    private void GenerateRelocationSuggestions(
        SchedulingResultDto result, int taskId, string taskTitle,
        DateTime scheduleStart, DateTime dueDate,
        List<Event> fixedEvents, List<TaskEvent> newTaskEvents,
        int dayStart, int dayEnd, double neededHours,
        HashSet<DateTime> examDays,
        HashSet<int> workEventIdSet, HashSet<int> personalEventIdSet,
        Dictionary<int, WorkEventRow> workEventLookup, Dictionary<int, PersonalEventRow> personalEventLookup)
    {
        for (var day = scheduleStart; day < dueDate; day = day.AddDays(1))
        {
            if (examDays.Contains(day.Date)) continue;

            var movableEvents = fixedEvents
                .Where(e => (e.From.Date == day.Date || (e.From < day.Date.AddDays(1) && e.To > day.Date))
                    && (workEventIdSet.Contains(e.EventId) || personalEventIdSet.Contains(e.EventId)))
                .ToList();

            foreach (var evt in movableEvents)
            {
                var evtDuration = (evt.To - evt.From).TotalHours;
                if (evtDuration >= neededHours * 0.5)
                {
                    var eventType = workEventIdSet.Contains(evt.EventId) ? "Work" : "Personal";
                    var eventTitle = GetEventTitle(evt.EventId, eventType, workEventLookup, personalEventLookup);
                    result.RelocationSuggestions.Add(new RelocationSuggestionDto
                    {
                        EventId = evt.EventId,
                        EventTitle = eventTitle,
                        EventType = eventType,
                        CurrentFrom = evt.From,
                        CurrentTo = evt.To,
                        BlockedTaskTitle = taskTitle,
                        Message = $"Moving \"{eventTitle}\" from {evt.From:ddd HH:mm}-{evt.To:HH:mm} would free up space for \"{taskTitle}\""
                    });
                }
            }

            if (result.RelocationSuggestions.Any(rs => rs.BlockedTaskTitle == taskTitle))
                break;
        }
    }

    private static string GetEventTitle(int eventId, string eventType,
        Dictionary<int, WorkEventRow> workLookup, Dictionary<int, PersonalEventRow> personalLookup)
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

        var prefs = await _dal.GetSchedPrefsByEmailAsync(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        var allIncompleteTasks = await _dal.GetAllIncompleteTasksAsync(email);
        var tasksWithDue = allIncompleteTasks.Where(t => t.DueDate.HasValue).ToList();

        var scheduledCount = tasksWithDue.Count(t => t.TaskEventCount > 0);
        var unscheduledCount = tasksWithDue.Count(t => t.TaskEventCount == 0);

        var scheduleStart = now.Date;
        var scheduleEnd = now.Date.AddDays(14);

        var taskEvents = await _dal.GetTaskEventsInRangeAsync(email, scheduleStart, scheduleEnd, "Scheduled");

        var fixedEvents = await GetExpandedEvents(email, scheduleStart, scheduleEnd);

        var resultDto = new SchedulingStatusDto
        {
            ScheduledCount = scheduledCount,
            UnscheduledCount = unscheduledCount
        };

        // Load typed event IDs for breakdown
        var workEvents = await _dal.GetWorkEventsByUserAsync(email);
        var workEventIds = workEvents.Select(w => w.EventId).ToList();
        var workEventIdSet = new HashSet<int>(workEventIds);
        var workEventLookup = workEvents.ToDictionary(w => w.EventId);
        var personalEvents = await _dal.GetPersonalEventsByUserAsync(email);
        var personalEventIds = personalEvents.Select(p => p.EventId).ToList();
        var personalEventIdSet = new HashSet<int>(personalEventIds);
        var personalEventLookup = personalEvents.ToDictionary(p => p.EventId);
        var classEventIds = await _dal.GetClassEventIdsByUserAsync(email);
        var classEventIdSet = new HashSet<int>(classEventIds);

        for (var day = scheduleStart; day < scheduleEnd; day = day.AddDays(1))
        {
            var dayEvents = fixedEvents.Where(e => e.From.Date == day.Date).ToList();
            double classHours = 0, workHours = 0, personalHours = 0;
            foreach (var evt in dayEvents)
            {
                var duration = Math.Max(0, (evt.To - evt.From).TotalHours);
                if (classEventIdSet.Contains(evt.EventId))
                    classHours += duration;
                else if (workEventIdSet.Contains(evt.EventId))
                    workHours += duration;
                else if (personalEventIdSet.Contains(evt.EventId))
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

            resultDto.DailyWorkload.Add(new DailyWorkloadDto
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
                resultDto.OverloadedDays.Add(day.ToString("yyyy-MM-dd"));
        }

        // Generate relocation suggestions for unscheduled tasks
        var unscheduledTasks = tasksWithDue.Where(t => t.TaskEventCount == 0).ToList();
        foreach (var task in unscheduledTasks)
        {
            var dueDate = task.DueDate!.Value;
            for (var day = scheduleStart; day < dueDate && day < scheduleEnd; day = day.AddDays(1))
            {
                var movableEvents = fixedEvents
                    .Where(e => e.From.Date == day.Date
                        && (workEventIdSet.Contains(e.EventId) || personalEventIdSet.Contains(e.EventId)))
                    .ToList();

                foreach (var evt in movableEvents)
                {
                    var evtDuration = (evt.To - evt.From).TotalHours;
                    if (evtDuration >= 1.0)
                    {
                        var eventType = workEventIdSet.Contains(evt.EventId) ? "Work" : "Personal";
                        var eventTitle = GetEventTitle(evt.EventId, eventType, workEventLookup, personalEventLookup);
                        resultDto.RelocationSuggestions.Add(new RelocationSuggestionDto
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

                if (resultDto.RelocationSuggestions.Any(rs => rs.BlockedTaskTitle == task.Title))
                    break;
            }
        }

        return resultDto;
    }

    private List<(DateTime From, DateTime To)> GetFreeSlots(
        DateTime day, List<Event> fixedEvents, List<TaskEvent> newTaskEvents,
        int dayStartHour, int dayEndHour)
    {
        var dayStartDt = day.Date.AddHours(dayStartHour);
        var dayEndDt = day.Date.AddHours(dayEndHour);

        if (day.Date == DateTime.Now.Date)
        {
            var now = DateTime.Now;
            var roundedNow = new DateTime(now.Year, now.Month, now.Day,
                now.Hour, now.Minute >= 30 ? 30 : 0, 0).AddMinutes(30);
            if (roundedNow > dayStartDt)
                dayStartDt = roundedNow;
        }

        if (dayStartDt >= dayEndDt) return new List<(DateTime, DateTime)>();

        var busy = new List<(DateTime From, DateTime To)>();

        foreach (var e in fixedEvents)
        {
            if (e.From.Date == day.Date || (e.From < dayEndDt && e.To > dayStartDt))
            {
                var from = e.From < dayStartDt ? dayStartDt : e.From;
                var to = e.To > dayEndDt ? dayEndDt : e.To;
                if (from < to) busy.Add((from, to));
            }
        }

        foreach (var te in newTaskEvents)
        {
            if (te.From.Date == day.Date)
            {
                var from = te.From < dayStartDt ? dayStartDt : te.From;
                var to = te.To > dayEndDt ? dayEndDt : te.To;
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
        var cursor = dayStartDt;
        foreach (var b in merged)
        {
            if (cursor < b.From)
                free.Add((cursor, b.From));
            cursor = b.To > cursor ? b.To : cursor;
        }
        if (cursor < dayEndDt)
            free.Add((cursor, dayEndDt));

        return free;
    }

    private double GetBlockedHours(DateTime day, List<Event> fixedEvents, int dayStartHour, int dayEndHour)
    {
        var dayStartDt = day.Date.AddHours(dayStartHour);
        var dayEndDt = day.Date.AddHours(dayEndHour);

        return fixedEvents
            .Where(e => e.From < dayEndDt && e.To > dayStartDt)
            .Sum(e =>
            {
                var from = e.From < dayStartDt ? dayStartDt : e.From;
                var to = e.To > dayEndDt ? dayEndDt : e.To;
                return Math.Max(0, (to - from).TotalHours);
            });
    }

    private async Task<List<Event>> GetExpandedEvents(
        string email, DateTime rangeStart, DateTime rangeEnd)
    {
        var events = await _dal.GetBaseEventsInRangeOrRecurringAsync(email, rangeStart, rangeEnd);

        var expanded = new List<Event>();
        foreach (var evt in events)
        {
            if (evt.From < rangeEnd && evt.To > rangeStart)
                expanded.Add(evt);

            if (evt.Recurring)
            {
                var duration = evt.To - evt.From;
                var endLimit = evt.RecurrenceEndDate.HasValue && evt.RecurrenceEndDate.Value < rangeEnd
                    ? evt.RecurrenceEndDate.Value : rangeEnd;
                var nextFrom = evt.From.AddDays(7);
                while (nextFrom < endLimit)
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

    /// <summary>
    /// Ensures the partner has a copy of a confirmed shared task and both users'
    /// calendars receive synchronized events at a mutually-free slot. Safe to call
    /// from any confirmation path (auto-share at creation, partner accepting).
    /// Returns true if a mutual free slot was found; false if the fallback mirror
    /// of the creator's schedule was used (or the task/member metadata is missing).
    /// </summary>
    public async Task<bool> EnsurePartnerCopyAndScheduleAsync(
        int originalTaskId, string creatorEmail, string partnerEmail)
    {
        var task = await _dal.GetTaskByIdAsync(originalTaskId);
        if (task == null) return false;

        if (!await _dal.UserCourseExistsAsync(partnerEmail, task.CourseId))
            await _dal.CreateUserCourseAsync(partnerEmail, task.CourseId);

        var existingCopyId = await _dal.GetSharedTaskMemberCopyTaskIdAsync(originalTaskId, partnerEmail);

        if (!existingCopyId.HasValue)
        {
            var copyTaskId = await _dal.CreateTaskAsync(
                task.CourseId, partnerEmail, task.Title, task.Type,
                task.EstimatedHours, task.DueDate, null,
                task.AllowSplitting, task.Priority, task.IsManualPriority);
            await _dal.UpdateSharedTaskMemberCopyTaskIdAsync(originalTaskId, partnerEmail, copyTaskId);
        }

        return await ScheduleSharedTaskAtCommonTimeAsync(originalTaskId, creatorEmail, partnerEmail);
    }

    public async Task<bool> ScheduleSharedTaskAtCommonTimeAsync(
        int originalTaskId, string creatorEmail, string partnerEmail)
    {
        var now = DateTime.Now;

        var originalTaskRow = await _dal.GetTaskByIdAsync(originalTaskId);
        if (originalTaskRow == null) return false;

        // Prefer the explicit CopyTaskId linkage on the member row so we never
        // mis-match against an unrelated task with the same title+course+due.
        TaskWithCourse? partnerTaskRow = null;
        var copyTaskId = await _dal.GetSharedTaskMemberCopyTaskIdAsync(originalTaskId, partnerEmail);
        if (copyTaskId.HasValue)
            partnerTaskRow = await _dal.GetTaskByIdAsync(copyTaskId.Value);
        if (partnerTaskRow == null)
        {
            var partnerMatch = await _dal.FindTaskByMatchAsync(partnerEmail, originalTaskRow.Title,
                originalTaskRow.CourseId, originalTaskRow.DueDate);
            if (partnerMatch != null)
                partnerTaskRow = await _dal.GetTaskByIdAsync(partnerMatch.TaskId);
        }
        if (partnerTaskRow == null) return false;

        // Step 1: Collect existing event IDs for the shared task
        var sharedTaskEvents = await _dal.GetTaskEventsByTaskIdsAndStatusesAsync(originalTaskRow.TaskId, partnerTaskRow.TaskId);
        var sharedTaskEventIds = sharedTaskEvents.Select(te => te.EventId).ToList();
        var sharedTaskEventIdSet = new HashSet<int>(sharedTaskEventIds);

        // Step 2: Load events
        var creatorPrefs = await _dal.GetSchedPrefsByEmailAsync(creatorEmail);
        var partnerPrefs = await _dal.GetSchedPrefsByEmailAsync(partnerEmail);
        int dayStart = Math.Max(creatorPrefs?.DayStartHour ?? 8, partnerPrefs?.DayStartHour ?? 8);
        int dayEnd = Math.Min(creatorPrefs?.DayEndHour ?? 22, partnerPrefs?.DayEndHour ?? 22);
        int maxContinuousMinutes = Math.Min(
            creatorPrefs?.MaxContinuousMinutes ?? 90,
            partnerPrefs?.MaxContinuousMinutes ?? 90);
        int breakMinutes = Math.Max(
            creatorPrefs?.BreakDurationMinutes ?? 15,
            partnerPrefs?.BreakDurationMinutes ?? 15);

        var dueDate = originalTaskRow.DueDate ?? now.AddDays(14);
        var scheduleStart = now.Date;
        var scheduleEnd = dueDate.Date.AddDays(1);

        var creatorEvents = (await GetExpandedEvents(creatorEmail, scheduleStart, scheduleEnd))
            .Where(e => !sharedTaskEventIdSet.Contains(e.EventId)).ToList();
        var partnerEvents = (await GetExpandedEvents(partnerEmail, scheduleStart, scheduleEnd))
            .Where(e => !sharedTaskEventIdSet.Contains(e.EventId)).ToList();

        // Step 3: Find mutual free slots
        double effectiveHours = (double)(originalTaskRow.EstimatedHours ?? 4);
        var remainingHours = effectiveHours;
        var newTaskEvents = new List<TaskEvent>();
        bool allowSplitting = originalTaskRow.AllowSplitting;

        for (var day = scheduleStart; day < scheduleEnd && remainingHours > 0; day = day.AddDays(1))
        {
            var dayStartTime = day.AddHours(dayStart);
            var dayEndTime = day.AddHours(dayEnd);

            if (day == now.Date && now > dayStartTime)
            {
                var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour,
                    now.Minute >= 30 ? 30 : 0, 0).AddMinutes(30);
                if (rounded > dayStartTime) dayStartTime = rounded;
            }
            if (dayStartTime >= dayEndTime) continue;

            var creatorBusy = GetDayBusyIntervals(creatorEvents, dayStartTime, dayEndTime);
            var partnerBusy = GetDayBusyIntervals(partnerEvents, dayStartTime, dayEndTime);

            foreach (var te in newTaskEvents.Where(t => t.From.Date == day.Date))
            {
                creatorBusy.Add((te.From, te.To));
                partnerBusy.Add((te.From, te.To));
            }

            var mutualFree = FindMutualFreeSlots(dayStartTime, dayEndTime, creatorBusy, partnerBusy);

            if (!allowSplitting)
            {
                var totalNeeded = effectiveHours +
                    (Math.Floor(effectiveHours / (maxContinuousMinutes / 60.0)) * (breakMinutes / 60.0));

                foreach (var slot in mutualFree)
                {
                    var slotDuration = (slot.To - slot.From).TotalHours;
                    if (slotDuration >= totalNeeded)
                    {
                        var sessions = SplitIntoSessionsWithBreaks(
                            slot.From, effectiveHours, maxContinuousMinutes, breakMinutes);
                        foreach (var session in sessions)
                        {
                            newTaskEvents.Add(new TaskEvent
                            {
                                Email = creatorEmail, From = session.From, To = session.To,
                                Recurring = false, TaskId = originalTaskRow.TaskId,
                                Priority = originalTaskRow.Priority ?? "Medium", Status = "NeedReview"
                            });
                            newTaskEvents.Add(new TaskEvent
                            {
                                Email = partnerEmail, From = session.From, To = session.To,
                                Recurring = false, TaskId = partnerTaskRow.TaskId,
                                Priority = partnerTaskRow.Priority ?? "Medium", Status = "NeedReview"
                            });
                        }
                        remainingHours = 0;
                        break;
                    }
                }
                if (remainingHours <= 0) break;
            }
            else
            {
                var maxContinuousHours = maxContinuousMinutes / 60.0;
                foreach (var slot in mutualFree)
                {
                    if (remainingHours <= 0) break;

                    var slotDuration = (slot.To - slot.From).TotalHours;
                    var canUse = Math.Min(slotDuration, Math.Min(remainingHours, maxContinuousHours));

                    if (canUse < 0.5) continue;
                    canUse = Math.Floor(canUse * 2) / 2;
                    if (canUse <= 0) continue;

                    var slotFrom = slot.From;
                    var slotTo = slotFrom.AddHours(canUse);

                    newTaskEvents.Add(new TaskEvent
                    {
                        Email = creatorEmail, From = slotFrom, To = slotTo,
                        Recurring = false, TaskId = originalTaskRow.TaskId,
                        Priority = originalTaskRow.Priority ?? "Medium", Status = "NeedReview"
                    });
                    newTaskEvents.Add(new TaskEvent
                    {
                        Email = partnerEmail, From = slotFrom, To = slotTo,
                        Recurring = false, TaskId = partnerTaskRow.TaskId,
                        Priority = partnerTaskRow.Priority ?? "Medium", Status = "NeedReview"
                    });

                    remainingHours -= canUse;
                }
            }
        }

        // Step 4: Apply. If we found common slots, swap both sides to those slots.
        // Otherwise fall back to mirroring whatever schedule the creator already
        // has onto the partner's calendar so the pair is at least synchronised
        // in time (the no-common-time notification warns about possible conflicts).
        if (newTaskEvents.Any())
        {
            foreach (var eventId in sharedTaskEventIds)
                await _dal.DeleteEventByIdAsync(eventId);

            foreach (var te in newTaskEvents)
                await _dal.CreateTaskEventAsync(te.Email, te.From, te.To, te.Recurring, null,
                    te.TaskId, te.Priority, te.Status);

            return true;
        }

        // Fallback: mirror creator's existing slots onto the partner so both users
        // see the task at the same time even when no mutual free slot exists.
        var creatorSharedTaskEvents = sharedTaskEvents
            .Where(te => te.TaskId == originalTaskRow.TaskId)
            .ToList();

        if (creatorSharedTaskEvents.Any())
        {
            // Remove any stale partner events for this task before mirroring.
            var partnerSharedEventIds = sharedTaskEvents
                .Where(te => te.TaskId == partnerTaskRow.TaskId)
                .Select(te => te.EventId)
                .ToList();
            foreach (var eventId in partnerSharedEventIds)
                await _dal.DeleteEventByIdAsync(eventId);

            foreach (var te in creatorSharedTaskEvents)
            {
                await _dal.CreateTaskEventAsync(
                    partnerEmail, te.From, te.To, te.Recurring, null,
                    partnerTaskRow.TaskId,
                    partnerTaskRow.Priority ?? te.Priority ?? "Medium",
                    "NeedReview");
            }

            return false; // signal "no mutual slot" so caller still warns users
        }

        return false;
    }

    private static List<(DateTime From, DateTime To)> GetDayBusyIntervals(
        List<Event> events, DateTime dayStart, DateTime dayEnd)
    {
        var busy = new List<(DateTime From, DateTime To)>();
        foreach (var e in events)
        {
            if (e.From < dayEnd && e.To > dayStart)
            {
                var from = e.From < dayStart ? dayStart : e.From;
                var to = e.To > dayEnd ? dayEnd : e.To;
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
        return merged;
    }

    private static List<(DateTime From, DateTime To)> FindMutualFreeSlots(
        DateTime dayStart, DateTime dayEnd,
        List<(DateTime From, DateTime To)> busyA,
        List<(DateTime From, DateTime To)> busyB)
    {
        var allBusy = busyA.Concat(busyB).OrderBy(b => b.From).ToList();
        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var b in allBusy)
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
            if (cursor < b.From) free.Add((cursor, b.From));
            cursor = b.To > cursor ? b.To : cursor;
        }
        if (cursor < dayEnd) free.Add((cursor, dayEnd));

        return free;
    }
}
