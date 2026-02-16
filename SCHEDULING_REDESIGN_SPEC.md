# Scheduling Engine & Stress Calculation Redesign Spec — SmartStudy

## Overview

This document specifies 10 major changes to the scheduling engine (`SchedulingService`) and stress/workload calculation (`StressService`). The changes aim to make the system smarter, more personal, and fully driven by user preferences.

---

## Change 1: Full Reschedule on Every Task Change

### Current Behavior
The scheduler already clears old TaskEvents and rebuilds, but it only finds empty slots — it doesn't displace existing lower-priority tasks.

### Required Change
Every time a task is created/updated/deleted, the scheduler runs **from scratch on all tasks** in priority order. A higher-priority task gets the best available slots, pushing lower-priority tasks to whatever remains.

### Technical Changes

**`SchedulingService.cs` — `ScheduleAllTasksAsync`:**
1. Delete **all** auto-scheduled TaskEvents (already exists ✓)
2. Sort tasks by priority score (already exists ✓)
3. Schedule in order — the first task gets the best slots
4. Lower-priority tasks get whatever is left

**`TasksController.cs` — automatic triggers:**
```csharp
// After each of the following operations — trigger full reschedule:
// POST   /api/tasks          (Create)
// PUT    /api/tasks/{id}     (Update — priority, dueDate, estimatedHours changed)
// DELETE /api/tasks/{id}     (Delete)
// POST   /api/tasks/{id}/complete (Complete)
```

Add a call to `_schedulingService.ScheduleAllTasksAsync(email)` at the end of each operation.

**`ExamsController.cs`** — same: creating/updating/deleting an exam triggers a full reschedule.

---

## Change 2: Default Behavior — Tasks Don't Split + 15-Minute Breaks

### Current Behavior
Tasks are automatically split into blocks of `maxContinuous` (90 min) and placed in any available slot across multiple days.

### Required Change
**Default**: A task is scheduled as a single continuous block. If the task duration exceeds `MaxContinuousMinutes`, a **15-minute break** is inserted, then the task continues immediately (no splitting across different days).

### Example
A 3-hour task with `MaxContinuousMinutes = 90`:
```
09:00-10:30  — Study session (90 min)
10:30-10:45  — Break (15 min)
10:45-12:15  — Continue studying (90 min)
```

### Technical Changes

**`StudentTask.cs` — new field:**
```csharp
public bool AllowSplitting { get; set; } = false;  // Default: don't split
```

**`SchedulingService.cs` — new scheduling logic:**
```
For each task:
  if (task.AllowSplitting == false):
    // Find a single continuous slot that fits the entire task (including breaks)
    totalNeeded = estimatedHours + (floor(estimatedHours / maxContinuousHours) * 0.25)
    Find a free window of totalNeeded hours
    If found:
      Schedule as a single block with internal breaks
    If not found:
      Add to UnscheduledTasks with reason "No continuous slot available"
  else (AllowSplitting == true):
    // Current logic — split across available slots
    Schedule in 30-minute blocks wherever there's free time
```

**Adding breaks to TaskEvents:**
```csharp
// Instead of creating one continuous TaskEvent, create a sequence:
var sessions = SplitIntoSessionsWithBreaks(slotFrom, totalHours, maxContinuousMinutes, breakMinutes: 15);
foreach (var session in sessions)
{
    newTaskEvents.Add(new TaskEvent { From = session.From, To = session.To, ... });
}
```

**Breaks are NOT events** — the break time is simply unscheduled (a 15-minute free gap between sessions).

---

## Change 3: Task Splitting Only When User Enables It

### Current Behavior
All tasks are automatically split across multiple slots/days.

### Required Change
`AllowSplitting` field on `StudentTask` (default: `false`). The user sets this when creating/editing a task.

### Technical Changes

**`CreateTaskDto.cs`:**
```csharp
public bool AllowSplitting { get; set; } = false;
```

**`UpdateTaskDto.cs`:**
```csharp
public bool? AllowSplitting { get; set; }
```

**`TaskDto.cs`:**
```csharp
public bool AllowSplitting { get; set; }
```

**Frontend — task create/edit form:**
Add a toggle/checkbox:
```
☐ Allow splitting this task across multiple days
```
Default: off.

---

## Change 4: Default Values Derived from User Preferences

### Current Behavior
Default values are hardcoded:
```csharp
int dayStart = prefs?.DayStartHour ?? 8;
double maxDaily = prefs?.MaxDailyStudyHours ?? 6.0;
```

### Required Change
All default values are pulled from user preferences set during onboarding. The hardcoded fallback remains only for cases where onboarding wasn't completed.

### Technical Changes

**`SchedulingPreferences.cs` — new fields:**
```csharp
public int BreakDurationMinutes { get; set; } = 15;          // Break between sessions
public double DefaultTaskEstimatedHours { get; set; } = 4.0; // Default estimated hours per task (see Change 9)
public double MaxDailyTotalHours { get; set; } = 14.0;       // Max total active hours per day (see Change 5)
public double ExamPrepHoursPerDay { get; set; } = 5.0;       // Exam prep hours per day (see Change 10)
public int ExamPrepDays { get; set; } = 3;                   // Exam prep days (see Change 10)
```

**`Onboarding2.html` / `onboarding.js`:**
Add new fields in Step 2:
- Break duration (default 15 minutes)
- Max total active hours per day (default 14)
- Exam prep hours per day (default 5)
- Exam prep days before exam (default 3)

---

## Change 5: Total Daily Load Calculation

### The Problem
The current system calculates stress based only on study hours. But a day with an 8-hour work shift + 3 hours of studying = 11 active hours, which can be stressful even though 3 study hours is below `MaxDailyStudyHours`.

### Proposed Solution: Two Combined Scores

#### New Setting: `MaxDailyTotalHours`
The user sets during onboarding: "How many total active hours per day can you handle?" (default: 14 hours = 24 - 8 sleep - 2 daily routine).

#### New Daily Load Formula:

```
# Step 1: Calculate hours by category
studyHours    = scheduled study hours for the day
workHours     = work event hours for the day
classHours    = class event hours for the day
personalHours = personal event hours for the day
totalHours    = studyHours + workHours + classHours + personalHours

# Step 2: Study Load score
studyLoad = (studyHours / MaxDailyStudyHours) * 100
# → Am I studying too much?

# Step 3: Total Load score
totalLoad = (totalHours / MaxDailyTotalHours) * 100
# → Is my day too packed overall?

# Step 4: Final score — the maximum of both
dailyScore = max(studyLoad, totalLoad)
```

#### Why the maximum?
- Day with 6h study + 0h work: `studyLoad=100%, totalLoad=43%` → **Score 100** (study overload)
- Day with 3h study + 8h work: `studyLoad=50%, totalLoad=79%` → **Score 79** (total overload)
- Day with 2h study + 2h work: `studyLoad=33%, totalLoad=29%` → **Score 33** (all good)

#### Impact on Scheduling:
The engine doesn't just check `dailyScheduledHours < maxDaily` but also:
```csharp
var totalDayHours = GetTotalEventHours(day, fixedEvents) + dailyScheduledHours[dayKey];
if (totalDayHours >= prefs.MaxDailyTotalHours) continue; // Skip overloaded day
```

### Technical Changes

**`SchedulingPreferences.cs`:**
```csharp
public double MaxDailyTotalHours { get; set; } = 14.0;
```

**`StressService.cs` — `GetWeeklyStressAsync`:**
```csharp
// New calculation:
double studyHours = dayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
double otherHours = dayEvents.Where(e => e is not TaskEvent)
                             .Sum(e => (e.To - e.From).TotalHours);
double totalHours = studyHours + otherHours;

double studyLoad = (studyHours / maxDailyStudy) * 100;
double totalLoad = (totalHours / maxDailyTotal) * 100;
double dayScore = Math.Min(100, Math.Max(studyLoad, totalLoad));
```

**`StressScoreDto` — new fields:**
```csharp
public double StudyLoad { get; set; }           // Study load score
public double TotalLoad { get; set; }           // Total load score
public double TotalScheduledHours { get; set; } // Total hours scheduled for the day
```

**`DailyWorkloadDto` — new fields:**
```csharp
public double StudyHours { get; set; }
public double WorkHours { get; set; }
public double ClassHours { get; set; }
public double PersonalHours { get; set; }
public double TotalHours { get; set; }
```

---

## Change 6: Immovable vs. Suggested-to-Move Events

### Current Behavior
All fixed events (classes, work, personal) are treated as immovable.

### Required Change
Event hierarchy:

| Event Type | Status | Action When No Room |
|------------|--------|---------------------|
| Exam Study | **Immovable** | Never moved |
| Classes | **Immovable** | Never moved |
| Work | **Protected** | System suggests moving, doesn't execute |
| Personal | **Protected** | System suggests moving, doesn't execute |
| Tasks | **Flexible** | Automatically rescheduled |

### Technical Changes

**`SchedulingResultDto.cs` — new field:**
```csharp
public List<RelocationSuggestionDto> RelocationSuggestions { get; set; } = new();
```

**New DTO:**
```csharp
public class RelocationSuggestionDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = null!;
    public string EventType { get; set; } = null!;       // "Work" | "Personal"
    public DateTime CurrentFrom { get; set; }
    public DateTime CurrentTo { get; set; }
    public string BlockedTaskTitle { get; set; } = null!; // Which task is blocked
    public string Message { get; set; } = null!;          // User-facing message
}
```

**`SchedulingService.cs` — new logic:**
```
For each task that couldn't be scheduled (UnscheduledTask):
  1. Find work/personal events that overlap with time slots that could have fit
  2. If found — add a RelocationSuggestion:
     "Moving [Personal Event] from Tue 14:00-16:00 would free up space for [Task Name]"
  3. Do NOT move it automatically — only suggest
```

**Frontend — notifications:**
When `RelocationSuggestions` is not empty, show a message to the user:
```
⚠️ No room found for "Assignment 3 in Course X"
  💡 Suggestion: Moving "Yoga" from Wed 16:00-17:30 would free up space
  [Move] [Dismiss]
```

---

## Change 7: Priority Boost for Shared Tasks

### Current Behavior
Shared tasks (`SharedTask`) receive no scheduling bonus.

### Required Change
A task marked as shared gets a priority bonus in the score calculation, since they're harder to reschedule (coordination with another person).

### Technical Changes

**`SchedulingService.cs` — updated priority formula:**
```csharp
// Updated priority score:
var isShared = task.SharedTask != null;
var sharedBonus = isShared ? 20 : 0;

var score = (1.0 / daysUntilDue) * 40
          + priorityWeight * 30
          + Math.Min(hours, 10) * 3
          + creditBonus                 // Change 8
          + sharedBonus;                // +20 for shared tasks
```

**Why 20 points?** Enough to push a shared task ahead of a regular task at the same priority level, but not enough to override an urgent task.

---

## Change 8: Course Credits Affect Priority Score

### Current Behavior
Priority score does not consider course credits.

### Required Change
Courses with more credits = their tasks are more important.

### Technical Changes

**`SchedulingService.cs` — updated priority formula:**
```csharp
// Add credit bonus:
var credits = task.Course?.Credits ?? 3;
var creditBonus = credits * 2.5;  // 3 credits = 7.5, 5 credits = 12.5

var score = (1.0 / daysUntilDue) * 40
          + priorityWeight * 30
          + Math.Min(hours, 10) * 3
          + creditBonus                 // New
          + sharedBonus;                // Change 7
```

**Full Updated Priority Formula:**
```
score = (1/daysUntilDue) * 40        — Urgency (0-400 for overdue tasks)
      + priorityWeight * 30          — Priority (30/60/90)
      + min(hours, 10) * 3           — Workload (0-30)
      + credits * 2.5                — Course importance (typical 5-15)
      + sharedBonus                  — Shared task bonus (0 or 20)
```

---

## Change 9: Default Estimated Hours per Task — 4 Hours, Configurable per Course

### Current Behavior
`EstimatedHours` defaults to null (fallback to 1 hour in the scheduler).

### Required Change
1. Global default: **4 hours** (set in `SchedulingPreferences`)
2. Per-course default: configurable in course settings
3. Once enough completed tasks exist (≥2) in a course — ML calculation takes over

### Hierarchy:
```
ML ratio (≥2 completed tasks in course)
  ↓ if not available
Course default (Course.DefaultTaskEstimatedHours)
  ↓ if not set
Global default (SchedulingPreferences.DefaultTaskEstimatedHours = 4.0)
  ↓ if no preferences
Hardcoded fallback = 4.0
```

### Technical Changes

**`Course.cs` — new field:**
```csharp
public double? DefaultTaskEstimatedHours { get; set; }  // null = use global
```

**`SchedulingPreferences.cs`:**
```csharp
public double DefaultTaskEstimatedHours { get; set; } = 4.0;
```

**`SchedulingService.cs` — updated hours calculation:**
```csharp
// For each task without explicit EstimatedHours:
double GetEffectiveHours(StudentTask task, Dictionary<int, double> courseRatios,
                         SchedulingPreferences prefs)
{
    double baseHours;

    if (task.EstimatedHours.HasValue && task.EstimatedHours > 0)
    {
        baseHours = (double)task.EstimatedHours;
    }
    else
    {
        // Default hierarchy
        baseHours = task.Course?.DefaultTaskEstimatedHours
                    ?? prefs?.DefaultTaskEstimatedHours
                    ?? 4.0;
    }

    // Apply ML ratio if available
    if (courseRatios.TryGetValue(task.CourseId, out var ratio))
        baseHours *= ratio;

    return baseHours;
}
```

**`UpdateCourseDto.cs` — new field:**
```csharp
public double? DefaultTaskEstimatedHours { get; set; }
```

**Frontend — course settings:**
Add field in course edit form:
```
Default estimated hours per task: [___] hours
(Used until enough tasks are completed for automatic calculation)
```

---

## Change 10: Exam Prep Time — Global + Per-Course Configuration

### Current Behavior
Hardcoded: 15 hours (5 hours × 3 days) for every exam.

### Required Change
1. **During onboarding**: User sets global values:
   - Prep hours per day (`ExamPrepHoursPerDay`, default: 5)
   - Prep days (`ExamPrepDays`, default: 3)
2. **In course settings**: Can override global values per course

### Hierarchy:
```
Course setting (Course.ExamPrepHoursPerDay + Course.ExamPrepDays)
  ↓ if not set
Global setting (SchedulingPreferences.ExamPrepHoursPerDay + ExamPrepDays)
  ↓ if no preferences
Hardcoded: 5 hours × 3 days
```

### Technical Changes

**`Course.cs` — new fields:**
```csharp
public double? ExamPrepHoursPerDay { get; set; }  // null = use global
public int? ExamPrepDays { get; set; }             // null = use global
```

**`SchedulingPreferences.cs`:**
```csharp
public double ExamPrepHoursPerDay { get; set; } = 5.0;
public int ExamPrepDays { get; set; } = 3;
```

**`SchedulingService.cs` — updated exam prep task creation:**
```csharp
foreach (var exam in exams)
{
    // Get values from course or global
    var prepHoursPerDay = exam.Course?.ExamPrepHoursPerDay
                          ?? prefs?.ExamPrepHoursPerDay ?? 5.0;
    var prepDays = exam.Course?.ExamPrepDays
                   ?? prefs?.ExamPrepDays ?? 3;
    var totalPrepHours = prepHoursPerDay * prepDays;

    existingStudyTask = new StudentTask
    {
        ...
        EstimatedHours = (decimal)totalPrepHours,
        ...
    };

    // Schedule prep days
    for (int d = 1; d <= prepDays; d++)
    {
        var prepDay = exam.Date.Date.AddDays(-d);
        // Schedule prepHoursPerDay hours per day
    }
}
```

**`Onboarding2.html`:**
```html
<div class="form-group">
    <label>Exam prep hours per day</label>
    <input type="number" id="examPrepHoursPerDay" value="5" min="1" max="12">
</div>
<div class="form-group">
    <label>Days before exam to start preparing</label>
    <input type="number" id="examPrepDays" value="3" min="1" max="14">
</div>
```

**`UpdateCourseDto.cs`:**
```csharp
public double? ExamPrepHoursPerDay { get; set; }
public int? ExamPrepDays { get; set; }
```

---

## Full Updated Priority Formula (After All Changes)

```
score = (1 / daysUntilDue) * 40            — Urgency (overdue = 1/0.1 = 400 points)
      + priorityWeight * 30                — User priority (High=90, Medium=60, Low=30)
      + min(effectiveHours, 10) * 3        — Workload scope (0-30 points)
      + credits * 2.5                      — Course importance (typical 5-15)
      + (isShared ? 20 : 0)               — Shared task bonus
```

### Calculation Examples:

| Task | Deadline | Priority | Hours | Credits | Shared | Score |
|------|----------|----------|-------|---------|--------|-------|
| Task A | Tomorrow (1 day) | High | 4h | 5 | Yes | 40+90+12+12.5+20 = **174.5** |
| Task B | In 3 days | High | 6h | 3 | No | 13.3+90+18+7.5+0 = **128.8** |
| Task C | In 1 week | Medium | 2h | 4 | Yes | 5.7+60+6+10+20 = **101.7** |
| Task D | In 1 week | Low | 3h | 3 | No | 5.7+30+9+7.5+0 = **52.2** |

---

## DB Schema Changes Summary

### Table `SmartStudy_SchedulingPreferences` — new columns:
```sql
ALTER TABLE SmartStudy_SchedulingPreferences ADD
    BreakDurationMinutes INT NOT NULL DEFAULT 15,
    DefaultTaskEstimatedHours FLOAT NOT NULL DEFAULT 4.0,
    MaxDailyTotalHours FLOAT NOT NULL DEFAULT 14.0,
    ExamPrepHoursPerDay FLOAT NOT NULL DEFAULT 5.0,
    ExamPrepDays INT NOT NULL DEFAULT 3;
```

### Table `SmartStudy_Courses` — new columns:
```sql
ALTER TABLE SmartStudy_Courses ADD
    DefaultTaskEstimatedHours FLOAT NULL,
    ExamPrepHoursPerDay FLOAT NULL,
    ExamPrepDays INT NULL;
```

### Table `SmartStudy_Tasks` — new column:
```sql
ALTER TABLE SmartStudy_Tasks ADD
    AllowSplitting BIT NOT NULL DEFAULT 0;
```

---

## File Changes Summary

| File | Changes |
|------|---------|
| `Models/SchedulingPreferences.cs` | 5 new fields |
| `Models/Course.cs` | 3 new fields |
| `Models/StudentTask.cs` | `AllowSplitting` field |
| `DTOs/SchedulingDtos.cs` | `RelocationSuggestionDto` + new fields in `DailyWorkloadDto` |
| `DTOs/TaskDtos.cs` | `AllowSplitting` in Create/Update/TaskDto |
| `DTOs/CourseDtos.cs` | Exam prep + default hours fields |
| `DTOs/DashboardDtos.cs` | `StudyLoad`, `TotalLoad` fields in StressScoreDto |
| `Services/SchedulingService.cs` | Major rewrite — continuous scheduling logic, breaks, relocation suggestions |
| `Services/StressService.cs` | New total load calculation |
| `Controllers/TasksController.cs` | Auto-reschedule trigger after CRUD |
| `Controllers/ExamsController.cs` | Auto-reschedule trigger after CRUD |
| `Controllers/CoursesController.cs` | New fields in Update |
| `Data/SmartStudyDbContext.cs` | Add new fields to migration |
| `Front/Pages/Onboarding2.html` | New onboarding fields |
| `Front/Script/modules/onboarding.js` | Handle new fields |
| `Front/CSS/app.css` | Relocation suggestion styling |

---

## Recommended Implementation Order

### Phase 1: Infrastructure (DB + Models)
1. Add fields to `SchedulingPreferences`, `Course`, `StudentTask`
2. Update `SmartStudyDbContext` + migration
3. Update DTOs

### Phase 2: Scheduling Engine
4. Rewrite `ScheduleAllTasksAsync` with continuous scheduling logic + breaks
5. Add `GetEffectiveHours` with default hierarchy
6. Update priority formula (credits + shared bonus)
7. Add relocation suggestion logic
8. Add automatic triggers in Controllers

### Phase 3: Stress Calculation
9. Rewrite `GetStressScoreAsync` with Total Load
10. Rewrite `GetWeeklyStressAsync` with dual calculation

### Phase 4: Frontend
11. Update onboarding (new fields)
12. Update task form (AllowSplitting)
13. Update course settings (exam prep + default hours)
14. Add UI for relocation suggestions
15. Update dashboard to display total load
