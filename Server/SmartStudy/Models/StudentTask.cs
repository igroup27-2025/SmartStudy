using SmartStudy.DAL;
using SmartStudy.Services;

namespace SmartStudy.Models;

// Task entity plus the auto-scheduling engine and ML/insights helpers folded in as static methods.
public class StudentTask
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public int? ParentTaskId { get; set; }
    public string Email { get; set; } = null!;
    public bool AllowSplitting { get; set; } = false;
    public bool IsManuallyPinned { get; set; } = false;
    public bool IsManualPriority { get; set; } = false;
    public string? MoodleId { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
    public User User { get; set; } = null!;
    public StudentTask? ParentTask { get; set; }
    public ICollection<StudentTask> SubTasks { get; set; } = new List<StudentTask>();
    public ICollection<TaskEvent> TaskEvents { get; set; } = new List<TaskEvent>();
    public SharedTask? SharedTask { get; set; }

    // ───── TasksBLL methods folded in ──────────────────────────────────

    // Returns the user's tasks (joined with course name), optionally filtered by course/completion.
    public static List<TaskWithCourse> GetByUser(string email, int? courseId = null, bool? completed = null)
    {
        DBservices db = new DBservices();
        return db.GetTasksByUser(email, courseId, completed);
    }

    // Loads a single task by ID joined with its course, or null if not found.
    public static TaskWithCourse? GetById(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetTaskById(taskId);
    }

    // Returns the immediate child subtasks of a parent task.
    public static List<TaskWithCourse> GetSubTasks(int parentTaskId)
    {
        DBservices db = new DBservices();
        return db.GetSubTasks(parentTaskId);
    }

    // Inserts a new task and returns its generated ID.
    public static int Create(int courseId, string email, string title, string type,
        decimal? estimatedHours, DateTime? dueDate, int? parentTaskId, bool allowSplitting,
        string? priority, bool isManualPriority, bool isManuallyPinned = false)
    {
        DBservices db = new DBservices();
        return db.CreateTask(courseId, email, title, type, estimatedHours, dueDate, parentTaskId,
            allowSplitting, priority, isManualPriority, isManuallyPinned);
    }

    // Updates any subset of a task's fields (each parameter optional).
    public static void Update(int taskId, int? courseId = null, string? title = null, string? type = null,
        decimal? estimatedHours = null, DateTime? dueDate = null, bool? isCompleted = null,
        bool? allowSplitting = null, bool? isManuallyPinned = null, string? priority = null,
        bool? isManualPriority = null, decimal? actualHours = null)
    {
        DBservices db = new DBservices();
        db.UpdateTask(taskId, courseId, title, type, estimatedHours, dueDate, isCompleted,
            allowSplitting, isManuallyPinned, priority, isManualPriority, actualHours);
    }

    // Deletes a task and any subtasks/task-events that reference it.
    public static void Delete(int taskId)
    {
        DBservices db = new DBservices();
        db.DeleteTask(taskId);
    }

    // Toggles completion state and optionally records actualHours for ML estimation tuning.
    public static void Complete(int taskId, bool isCompleted, decimal? actualHours = null)
    {
        DBservices db = new DBservices();
        db.CompleteTask(taskId, isCompleted, actualHours);
    }

    // Returns the scheduled study-block events for a task.
    public static List<TaskEventInfo> GetTaskEvents(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetTaskEvents(taskId);
    }

    // Returns sharing info (creator, members, status) if the task is shared, else null.
    public static SharedTaskInfo? GetSharedInfo(int taskId)
    {
        DBservices db = new DBservices();
        return db.GetSharedInfo(taskId);
    }

    // Returns true if every subtask of the parent is completed (used to auto-complete the parent).
    public static bool CheckAllSiblingsComplete(int parentTaskId)
    {
        DBservices db = new DBservices();
        return db.CheckAllSiblingsComplete(parentTaskId);
    }

    // Returns completed-task estimated/actual pairs for one course (used to suggest hour adjustments).
    public static List<MLDataRow> GetMLData(string email, int courseId)
    {
        DBservices db = new DBservices();
        return db.GetMLData(email, courseId);
    }

    // Returns per-course estimation accuracy stats from completed tasks.
    public static List<MLInsightRow> GetMLInsights(string email)
    {
        DBservices db = new DBservices();
        return db.GetMLInsights(email);
    }

    // ───── SchedulingBLL methods ──────────────────────────────────

    // Approves a task's scheduled events and prunes any whose end-time is already in the past.
    public static (int ApprovedCount, int RemovedPast) ApproveTaskEvents(int taskId, string email, DateTime now)
    {
        DBservices db = new DBservices();
        return db.ApproveTaskEvents(taskId, email, now);
    }

    // ───── SchedulingService (core scheduling engine) ─────────────

    private const int SlotMinutes = 30;

    // Greedy auto-scheduler: places study blocks for incomplete tasks into free 30-min calendar slots.
    public static SchedulingResultDto ScheduleAll(string email)
    {
        var db = new DBservices();
        var now = DateTime.Now;
        var result = new SchedulingResultDto();

        var prefs = db.GetSchedPrefsByEmail(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;
        int maxContinuousMinutes = prefs?.MaxContinuousMinutes ?? 90;
        int breakMinutes = prefs?.BreakDurationMinutes ?? 15;
        double examPrepHoursPerDay = prefs?.ExamPrepHoursPerDay ?? 5.0;
        int examPrepDays = prefs?.ExamPrepDays ?? 3;

        var allLeafTasks = db.GetIncompleteLeafTasks(email);

        var scheduleStart = now.Date;
        var maxDueDate = allLeafTasks.Any() ? allLeafTasks.Max(t => t.DueDate!.Value) : now.AddDays(14);

        var upcomingExams = db.GetExamsForScheduling(email, scheduleStart, now.AddYears(1));
        if (upcomingExams.Any())
        {
            var maxExamDate = upcomingExams.Max(e => e.Date);
            if (maxExamDate > maxDueDate) maxDueDate = maxExamDate;
        }

        if (maxDueDate < now.AddDays(7)) maxDueDate = now.AddDays(7);
        var scheduleEnd = maxDueDate.Date.AddDays(1);

        var pinnedTaskIds = db.GetPinnedTaskIds(email);
        var pinnedTaskIdSet = new HashSet<int>(pinnedTaskIds);

        var needReviewTaskIds = db.GetNeedReviewTaskIds(email);
        var needReviewTaskIdSet = new HashSet<int>(needReviewTaskIds);

        var existingTaskEvents = db.GetTaskEventsByUserAndStatus(email, "Scheduled", "Partial");

        var existingTaskEventIds = existingTaskEvents
            .Where(te => !pinnedTaskIdSet.Contains(te.TaskId))
            .Select(te => te.EventId)
            .ToList();

        if (existingTaskEventIds.Any())
        {
            foreach (var eventId in existingTaskEventIds)
                db.DeleteEventById(eventId);
        }

        var pinnedTaskEventsList = existingTaskEvents
            .Where(te => pinnedTaskIdSet.Contains(te.TaskId))
            .ToList();

        var fixedEvents = GetExpandedEvents(db, email, scheduleStart, scheduleEnd);

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

        var classEventIds = db.GetClassEventIdsByUser(email);
        var classEventIdSet = new HashSet<int>(classEventIds);
        var workEvents = db.GetWorkEventsByUser(email);
        var workEventIds = workEvents.Select(w => w.EventId).ToList();
        var workEventIdSet = new HashSet<int>(workEventIds);
        var personalEvents = db.GetPersonalEventsByUser(email);
        var personalEventIds = personalEvents.Select(p => p.EventId).ToList();
        var personalEventIdSet = new HashSet<int>(personalEventIds);
        var workEventLookup = workEvents.ToDictionary(w => w.EventId);
        var personalEventLookup = personalEvents.ToDictionary(p => p.EventId);

        var typedEvents = fixedEvents;

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

        var exams = upcomingExams;
        var examDays = new HashSet<DateTime>(exams.Select(e => e.Date.Date));

        var examStudyTasks = new List<SimpleTaskRow>();
        foreach (var exam in exams)
        {
            if (exam.Date.Date <= now.Date) continue;

            var coursePrepHoursPerDay = exam.CourseExamPrepHoursPerDay ?? examPrepHoursPerDay;
            var coursePrepDays = exam.CourseExamPrepDays ?? examPrepDays;
            var totalPrepHours = coursePrepHoursPerDay * coursePrepDays;

            var existingStudyTask = db.GetStudyForExamTask(email, exam.CourseId, exam.Date.Date);

            if (existingStudyTask == null)
            {
                var courseName = exam.CourseName;
                var taskId = db.CreateTask(exam.CourseId, email,
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
                    db.UpdateTask(existingStudyTask.TaskId, estimatedHours: (decimal)totalPrepHours);
                    existingStudyTask.EstimatedHours = (decimal)totalPrepHours;
                }
                if (!existingStudyTask.AllowSplitting)
                {
                    db.UpdateTask(existingStudyTask.TaskId, allowSplitting: true);
                    existingStudyTask.AllowSplitting = true;
                }
            }

            examStudyTasks.Add(existingStudyTask);
        }

        var validExamStudyIds = examStudyTasks.Select(t => t.TaskId).ToHashSet();
        var allStudyTaskIds = db.GetOrphanedStudyTaskIds(email);
        var orphanedIds = allStudyTaskIds.Where(id => !validExamStudyIds.Contains(id)).ToList();

        foreach (var orphanId in orphanedIds)
            db.DeleteTaskWithEvents(orphanId);

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

        var completedForML = db.GetCompletedTasksForML(email);
        var courseRatios = completedForML
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.ActualHours / t.EstimatedHours));

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
                db.UpdateTaskPriority(t.TaskId, priority);
            }
            else
            {
                finalPriority = t.Priority ?? priority;
            }

            return new { Task = t, Score = score, Priority = finalPriority, EffectiveHours = hours };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        var dailyScheduledStudyHours = new Dictionary<DateTime, double>();
        var newTaskEvents = new List<TaskEvent>();

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

        foreach (var te in newTaskEvents)
        {
            db.CreateTaskEvent(te.Email, te.From, te.To, te.Recurring, null,
                te.TaskId, te.Priority, te.Status);
        }

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

    // Returns daily-workload, overloaded-day, and relocation-suggestion data for the dashboard.
    public static SchedulingStatusDto GetSchedulingStatus(string email)
    {
        var db = new DBservices();
        var now = DateTime.Now;

        var prefs = db.GetSchedPrefsByEmail(email);
        int dayStart = prefs?.DayStartHour ?? 8;
        int dayEnd = prefs?.DayEndHour ?? 22;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        var allIncompleteTasks = db.GetAllIncompleteTasks(email);
        var tasksWithDue = allIncompleteTasks.Where(t => t.DueDate.HasValue).ToList();

        var scheduledCount = tasksWithDue.Count(t => t.TaskEventCount > 0);
        var unscheduledCount = tasksWithDue.Count(t => t.TaskEventCount == 0);

        var scheduleStart = now.Date;
        var scheduleEnd = now.Date.AddDays(14);

        var taskEvents = db.GetTaskEventsInRange(email, scheduleStart, scheduleEnd, "Scheduled");

        var fixedEvents = GetExpandedEvents(db, email, scheduleStart, scheduleEnd);

        var resultDto = new SchedulingStatusDto
        {
            ScheduledCount = scheduledCount,
            UnscheduledCount = unscheduledCount
        };

        var workEvents = db.GetWorkEventsByUser(email);
        var workEventIds = workEvents.Select(w => w.EventId).ToList();
        var workEventIdSet = new HashSet<int>(workEventIds);
        var workEventLookup = workEvents.ToDictionary(w => w.EventId);
        var personalEvents = db.GetPersonalEventsByUser(email);
        var personalEventIds = personalEvents.Select(p => p.EventId).ToList();
        var personalEventIdSet = new HashSet<int>(personalEventIds);
        var personalEventLookup = personalEvents.ToDictionary(p => p.EventId);
        var classEventIds = db.GetClassEventIdsByUser(email);
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
                        var cleanTaskTitle = TextHelpers.StripGcalTag(task.Title) ?? task.Title;
                        resultDto.RelocationSuggestions.Add(new RelocationSuggestionDto
                        {
                            EventId = evt.EventId,
                            EventTitle = eventTitle,
                            EventType = eventType,
                            CurrentFrom = evt.From,
                            CurrentTo = evt.To,
                            BlockedTaskTitle = cleanTaskTitle,
                            Message = $"Moving \"{eventTitle}\" from {evt.From:ddd HH:mm}-{evt.To:HH:mm} would free up space for \"{cleanTaskTitle}\""
                        });
                        break;
                    }
                }

                var cleanTaskTitleForCheck = TextHelpers.StripGcalTag(task.Title) ?? task.Title;
                if (resultDto.RelocationSuggestions.Any(rs => rs.BlockedTaskTitle == cleanTaskTitleForCheck))
                    break;
            }
        }

        return resultDto;
    }

    // After a shared task is confirmed, creates the partner's copy and tries to schedule both at a common time.
    public static bool EnsurePartnerCopyAndSchedule(int originalTaskId, string creatorEmail, string partnerEmail)
    {
        var db = new DBservices();
        var task = db.GetTaskById(originalTaskId);
        if (task == null) return false;

        if (!db.UserCourseExists(partnerEmail, task.CourseId))
            db.CreateUserCourse(partnerEmail, task.CourseId);

        var existingCopyId = db.GetSharedTaskMemberCopyTaskId(originalTaskId, partnerEmail);

        if (!existingCopyId.HasValue)
        {
            var copyTaskId = db.CreateTask(
                task.CourseId, partnerEmail, task.Title, task.Type,
                task.EstimatedHours, task.DueDate, null,
                task.AllowSplitting, task.Priority, task.IsManualPriority);
            db.UpdateSharedTaskMemberCopyTaskId(originalTaskId, partnerEmail, copyTaskId);
        }

        return ScheduleSharedTaskAtCommonTime(originalTaskId, creatorEmail, partnerEmail);
    }

    // Finds an overlapping free slot for both users and pins matching task events on each calendar.
    public static bool ScheduleSharedTaskAtCommonTime(int originalTaskId, string creatorEmail, string partnerEmail)
    {
        var db = new DBservices();
        var now = DateTime.Now;

        var originalTaskRow = db.GetTaskById(originalTaskId);
        if (originalTaskRow == null) return false;

        TaskWithCourse? partnerTaskRow = null;
        var copyTaskId = db.GetSharedTaskMemberCopyTaskId(originalTaskId, partnerEmail);
        if (copyTaskId.HasValue)
            partnerTaskRow = db.GetTaskById(copyTaskId.Value);
        if (partnerTaskRow == null)
        {
            var partnerMatch = db.FindTaskByMatch(partnerEmail, originalTaskRow.Title,
                originalTaskRow.CourseId, originalTaskRow.DueDate);
            if (partnerMatch != null)
                partnerTaskRow = db.GetTaskById(partnerMatch.TaskId);
        }
        if (partnerTaskRow == null) return false;

        var sharedTaskEvents = db.GetTaskEventsByTaskIdsAndStatuses(originalTaskRow.TaskId, partnerTaskRow.TaskId);
        var sharedTaskEventIds = sharedTaskEvents.Select(te => te.EventId).ToList();
        var sharedTaskEventIdSet = new HashSet<int>(sharedTaskEventIds);

        var creatorPrefs = db.GetSchedPrefsByEmail(creatorEmail);
        var partnerPrefs = db.GetSchedPrefsByEmail(partnerEmail);
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

        var creatorEvents = GetExpandedEvents(db, creatorEmail, scheduleStart, scheduleEnd)
            .Where(e => !sharedTaskEventIdSet.Contains(e.EventId)).ToList();
        var partnerEvents = GetExpandedEvents(db, partnerEmail, scheduleStart, scheduleEnd)
            .Where(e => !sharedTaskEventIdSet.Contains(e.EventId)).ToList();

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

        if (newTaskEvents.Any())
        {
            foreach (var eventId in sharedTaskEventIds)
                db.DeleteEventById(eventId);

            foreach (var te in newTaskEvents)
                db.CreateTaskEvent(te.Email, te.From, te.To, te.Recurring, null,
                    te.TaskId, te.Priority, te.Status);

            return true;
        }

        var creatorSharedTaskEvents = sharedTaskEvents
            .Where(te => te.TaskId == originalTaskRow.TaskId)
            .ToList();

        if (creatorSharedTaskEvents.Any())
        {
            var partnerSharedEventIds = sharedTaskEvents
                .Where(te => te.TaskId == partnerTaskRow.TaskId)
                .Select(te => te.EventId)
                .ToList();
            foreach (var eventId in partnerSharedEventIds)
                db.DeleteEventById(eventId);

            foreach (var te in creatorSharedTaskEvents)
            {
                db.CreateTaskEvent(
                    partnerEmail, te.From, te.To, te.Recurring, null,
                    partnerTaskRow.TaskId,
                    partnerTaskRow.Priority ?? te.Priority ?? "Medium",
                    "NeedReview");
            }

            return false;
        }

        return false;
    }

    // ───── WeeklySuggestionService (moved here as task-related logic) ────

    // Returns per-day "what to focus on" suggestions for the next 7 days.
    public static WeeklySuggestionsDto GetWeeklySuggestions(string email)
    {
        var db = new DBservices();
        var now = DateTime.Now;
        var weekEnd = now.Date.AddDays(7);
        var result = new WeeklySuggestionsDto();

        var allTasks = db.GetIncompleteLeafTasks(email);
        var tasks = allTasks
            .Where(t => t.DueDate.HasValue && t.DueDate <= weekEnd.AddDays(7))
            .OrderBy(t => t.DueDate)
            .ToList();

        var totalHoursNeeded = tasks.Sum(t => (double)(t.EstimatedHours ?? 1));
        result.TotalStudyHoursNeeded = Math.Round(totalHoursNeeded, 1);

        var events = db.GetEventsInDateRange(email, now.Date, weekEnd);
        var busyHours = events.Sum(e => (e.To - e.From).TotalHours);
        var totalDayHours = 7 * 14.0;
        result.AvailableStudyHours = Math.Round(Math.Max(0, totalDayHours - busyHours), 1);

        var stress = User.GetStressScore(email);

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

        var tasksByDay = tasks
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

        var urgentTasks = tasks.Where(t => t.DueDate.HasValue && (t.DueDate.Value - now).TotalHours <= 48).ToList();
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

        result.FocusTasks = tasks
            .Where(t => t.DueDate.HasValue && t.DueDate > now)
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : 1)
            .Take(3)
            .Select(t => new FocusTaskDto
            {
                TaskId = t.TaskId,
                Title = t.Title,
                CourseName = t.CourseName,
                HoursNeeded = (double)(t.EstimatedHours ?? 1),
                DaysUntilDue = Math.Max(0, (int)(t.DueDate!.Value.Date - now.Date).TotalDays),
                Priority = t.Priority
            })
            .ToList();

        return result;
    }

    // ───── Private scheduling helpers ─────────────────────────────

    // Returns the user-specific estimated hours for a task, applying ML adjustment ratio if available.
    private static double GetEffectiveHours(SchedulingTaskRow task, Dictionary<int, double> courseRatios, SchedulingPreferences? prefs)
    {
        double baseHours;

        if (task.EstimatedHours.HasValue && task.EstimatedHours > 0)
            baseHours = (double)task.EstimatedHours;
        else
            baseHours = task.DefaultTaskEstimatedHours
                        ?? prefs?.DefaultTaskEstimatedHours
                        ?? 4.0;

        if (courseRatios.TryGetValue(task.CourseId, out var ratio))
            baseHours *= ratio;

        return baseHours;
    }

    // Breaks a long study block into sub-sessions separated by short breaks per user prefs.
    private static List<(DateTime From, DateTime To)> SplitIntoSessionsWithBreaks(
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
                cursor = cursor.AddMinutes(breakMinutes);
        }

        return sessions;
    }

    // Builds suggestions to move events off overloaded days into free slots elsewhere.
    private static void GenerateRelocationSuggestions(
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
                    var cleanTaskTitle = TextHelpers.StripGcalTag(taskTitle) ?? taskTitle;
                    result.RelocationSuggestions.Add(new RelocationSuggestionDto
                    {
                        EventId = evt.EventId,
                        EventTitle = eventTitle,
                        EventType = eventType,
                        CurrentFrom = evt.From,
                        CurrentTo = evt.To,
                        BlockedTaskTitle = cleanTaskTitle,
                        Message = $"Moving \"{eventTitle}\" from {evt.From:ddd HH:mm}-{evt.To:HH:mm} would free up space for \"{cleanTaskTitle}\""
                    });
                }
            }

            var cleanTaskTitleForCheck = TextHelpers.StripGcalTag(taskTitle) ?? taskTitle;
            if (result.RelocationSuggestions.Any(rs => rs.BlockedTaskTitle == cleanTaskTitleForCheck))
                break;
        }
    }

    // Builds a human-readable title for an event based on its subtype and linked entities.
    private static string GetEventTitle(int eventId, string eventType,
        Dictionary<int, WorkEventRow> workLookup, Dictionary<int, PersonalEventRow> personalLookup)
    {
        if (eventType == "Work" && workLookup.TryGetValue(eventId, out var work))
            return !string.IsNullOrWhiteSpace(work.WorkPlace) ? work.WorkPlace : "Work";
        if (eventType == "Personal" && personalLookup.TryGetValue(eventId, out var personal))
        {
            var stripped = TextHelpers.StripGcalTag(personal.Description);
            if (!string.IsNullOrWhiteSpace(stripped)) return stripped!;
            return !string.IsNullOrWhiteSpace(personal.Type) ? personal.Type : "Personal";
        }
        return eventType;
    }

    // Returns 30-minute free slots in the user's calendar within the day window, respecting fixed events.
    private static List<(DateTime From, DateTime To)> GetFreeSlots(
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

    // Sums the hours blocked by class/work/personal events on a single day.
    private static double GetBlockedHours(DateTime day, List<Event> fixedEvents, int dayStartHour, int dayEndHour)
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

    // Returns events in a date range with weekly-recurring instances expanded into concrete dates.
    internal static List<Event> GetExpandedEvents(DBservices db, string email, DateTime rangeStart, DateTime rangeEnd)
    {
        var events = db.GetBaseEventsInRangeOrRecurring(email, rangeStart, rangeEnd);

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

    // Returns sorted, merged busy intervals for one user on one day.
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

    // Subtracts the union of two users' busy intervals from a day window to find common free time.
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

// ───── Task DTOs (from TaskDtos.cs) ────────────────────────────────

// Wire-format payload for the Tasks API including subtasks and scheduling info.
public class TaskDto
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }

    // Sub-task fields
    public int? ParentTaskId { get; set; }
    public List<TaskDto>? SubTasks { get; set; }
    public int SubTaskCount { get; set; }
    public int CompletedSubTaskCount { get; set; }
    public double SubTaskProgress { get; set; }

    // Shared task fields
    public bool IsShared { get; set; }
    public string? SharedStatus { get; set; }
    public string? SharedWithName { get; set; }
    public string? SharedWithEmail { get; set; }

    // Scheduling fields
    public DateTime? ScheduledDate { get; set; }
    public string SchedulingStatus { get; set; } = "Unscheduled"; // "Scheduled" | "Unscheduled" | "Partial"
    public List<TaskSlotDto>? ScheduledSlots { get; set; }
    public bool AllowSplitting { get; set; }
    public bool IsManuallyPinned { get; set; }
    public bool IsManualPriority { get; set; }
}

// One scheduled study slot inside a TaskDto.
public class TaskSlotDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

// Request body for POST /api/tasks.
public class CreateTaskDto
{
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public int? ParentTaskId { get; set; }
    public bool AllowSplitting { get; set; } = false;
    public string? Priority { get; set; }
}

// Request body for POST /api/tasks/{id}/complete (carries actualHours).
public class CompleteTaskDto
{
    public decimal? ActualHours { get; set; }
}

// Request body for POST /api/tasks/{id}/split — list of new subtasks to create.
public class SplitTaskDto
{
    public List<SubTaskDefinition> SubTasks { get; set; } = new();
}

// One new subtask description inside a SplitTaskDto.
public class SubTaskDefinition
{
    public string Title { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
}

// Request body for PUT /api/tasks/{id} — all fields optional.
public class UpdateTaskDto
{
    public int? CourseId { get; set; }
    public string? Title { get; set; }
    public string? Type { get; set; }
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool? IsCompleted { get; set; }
    public bool? AllowSplitting { get; set; }
    public bool? IsManuallyPinned { get; set; }
    public string? Priority { get; set; }
}

// ───── Scheduling DTOs (from SchedulingDtos.cs) ────────────────────

// Result returned by POST /api/scheduling/run — counts and per-task scheduling outcome.
public class SchedulingResultDto
{
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
    public List<ScheduledTaskDto> ScheduledTasks { get; set; } = new();
    public List<UnscheduledTaskDto> UnscheduledTasks { get; set; } = new();
    public List<RelocationSuggestionDto> RelocationSuggestions { get; set; } = new();
}

// One successfully scheduled task in a SchedulingResultDto.
public class ScheduledTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public List<ScheduledSlotDto> Slots { get; set; } = new();
}

// One task the scheduler couldn't fit, with the reason recorded.
public class UnscheduledTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string Reason { get; set; } = null!;
}

// One scheduled slot within a ScheduledTaskDto.
public class ScheduledSlotDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

// One day's workload bucket (study + total hours) for the dashboard chart.
public class DailyWorkloadDto
{
    public DateTime Date { get; set; }
    public double ScheduledHours { get; set; }
    public double AvailableHours { get; set; }
    public bool IsOverloaded { get; set; }
    public double StudyHours { get; set; }
    public double WorkHours { get; set; }
    public double ClassHours { get; set; }
    public double PersonalHours { get; set; }
    public double TotalHours { get; set; }
}

// One "move event X to free slot Y" suggestion shown to relieve an overloaded day.
public class RelocationSuggestionDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public DateTime CurrentFrom { get; set; }
    public DateTime CurrentTo { get; set; }
    public string BlockedTaskTitle { get; set; } = null!;
    public string Message { get; set; } = null!;
}

// Aggregated scheduling-engine status returned by GET /api/scheduling/status.
public class SchedulingStatusDto
{
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<DailyWorkloadDto> DailyWorkload { get; set; } = new();
    public List<string> OverloadedDays { get; set; } = new();
    public List<RelocationSuggestionDto> RelocationSuggestions { get; set; } = new();
}

// ───── Weekly suggestion DTOs (from WeeklySuggestionDtos.cs) ───────

// Weekly suggestions payload returned by GET /api/dashboard/weekly-suggestions.
public class WeeklySuggestionsDto
{
    public List<SuggestionDto> Suggestions { get; set; } = new();
    public List<FocusTaskDto> FocusTasks { get; set; } = new();
    public double TotalStudyHoursNeeded { get; set; }
    public double AvailableStudyHours { get; set; }
}

// One per-day suggestion item inside a WeeklySuggestionsDto.
public class SuggestionDto
{
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Icon { get; set; } = null!;
}

// "Top focus task" recommendation inside a SuggestionDto.
public class FocusTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public double HoursNeeded { get; set; }
    public int DaysUntilDue { get; set; }
    public string? Priority { get; set; }
}
