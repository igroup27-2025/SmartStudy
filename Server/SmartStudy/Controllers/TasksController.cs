using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

// API endpoints for task CRUD, completion, splitting, ML hour suggestions, and insights.
[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Lists tasks for the user, optionally filtered by course and completion state.
    [HttpGet]
    public IActionResult GetAll([FromQuery] int? courseId, [FromQuery] bool? completed)
    {
        var email = GetEmail();
        var tasks = StudentTask.GetByUser(email, courseId, completed);

        var dtos = new List<TaskDto>();
        foreach (var t in tasks)
        {
            dtos.Add(BuildTaskDto(t));
        }
        return Ok(dtos);
    }

    // Returns one task by ID if owned by the current user.
    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        var email = GetEmail();
        var task = StudentTask.GetById(id);
        if (task == null || task.Email != email) return NotFound();

        return Ok(BuildTaskDto(task));
    }

    // Creates a task (auto-shares with study partner if course is shared by default) and re-schedules.
    [HttpPost]
    public IActionResult Create([FromBody] CreateTaskDto dto)
    {
        var email = GetEmail();

        string? priority = null;
        bool isManualPriority = false;
        if (!string.IsNullOrEmpty(dto.Priority) && dto.Priority != "Auto")
        {
            priority = dto.Priority;
            isManualPriority = true;
        }

        var courseId = dto.CourseId;
        var dueDate = dto.DueDate;

        // If sub-task, inherit course/due date from parent
        if (dto.ParentTaskId.HasValue)
        {
            var parent = StudentTask.GetById(dto.ParentTaskId.Value);
            if (parent == null || parent.Email != email)
                return BadRequest(new { message = "Parent task not found" });
            if (courseId == 0) courseId = parent.CourseId;
            if (!dueDate.HasValue) dueDate = parent.DueDate;
        }

        var taskId = StudentTask.Create(courseId, email, dto.Title, dto.Type,
            dto.EstimatedHours, dueDate, dto.ParentTaskId, dto.AllowSplitting, priority, isManualPriority);

        string? autoSharedPartner = null;
        bool autoSharedConfirmed = false;

        var userCourse = UserCourse.Get(email, courseId);
        if (userCourse?.SharedByDefault == true && !string.IsNullOrEmpty(userCourse.StudyPartnerEmail))
        {
            SharedTask.Create(taskId, email, "Pending");
            SharedTask.CreateMember(taskId, email, "Accepted", DateTime.UtcNow);

            var partnerUserCourse = UserCourse.Get(userCourse.StudyPartnerEmail, courseId);
            var autoApproved = partnerUserCourse?.CourseShareApproved == true;

            SharedTask.CreateMember(taskId, userCourse.StudyPartnerEmail,
                autoApproved ? "Accepted" : "Pending",
                autoApproved ? DateTime.UtcNow : null);

            if (autoApproved)
            {
                SharedTask.UpdateStatus(taskId, "Confirmed");
                autoSharedConfirmed = true;
                autoSharedPartner = userCourse.StudyPartnerEmail;
            }
        }

        if (autoSharedConfirmed && autoSharedPartner != null)
        {
            StudentTask.EnsurePartnerCopyAndSchedule(taskId, email, autoSharedPartner);
            StudentTask.ScheduleAll(email);
            StudentTask.ScheduleAll(autoSharedPartner);
        }
        else
        {
            StudentTask.ScheduleAll(email);
        }

        var reloaded = StudentTask.GetById(taskId);
        return CreatedAtAction(nameof(Get), new { id = taskId }, reloaded != null ? BuildTaskDto(reloaded) : null);
    }

    // Updates a task's fields, normalizes Auto-vs-manual priority, and re-schedules.
    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var email = GetEmail();
        var task = StudentTask.GetById(id);
        if (task == null || task.Email != email) return NotFound();

        string? priority = dto.Priority;
        bool? isManualPriority = null;
        if (dto.Priority != null)
        {
            if (dto.Priority == "Auto")
            {
                isManualPriority = false;
                priority = null;
            }
            else
            {
                isManualPriority = true;
            }
        }

        StudentTask.Update(id, dto.CourseId, dto.Title, dto.Type, dto.EstimatedHours,
            dto.DueDate, dto.IsCompleted, dto.AllowSplitting, dto.IsManuallyPinned, priority, isManualPriority);

        // If this is a confirmed shared task, sync the partner's copy and reschedule both at the same time.
        var sharedInfo = StudentTask.GetSharedInfo(id);
        if (sharedInfo?.SharedStatus == "Confirmed")
        {
            var originalTaskId = sharedInfo.TaskId;
            var creatorEmail = sharedInfo.CreatedByEmail;
            var partnerMember = sharedInfo.Members.FirstOrDefault(m =>
                !string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase));
            var partnerEmail = partnerMember?.Email;

            if (partnerEmail != null)
            {
                // When the creator edits the original task, propagate definition changes to the partner's copy.
                if (string.Equals(email, creatorEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var copyTaskId = SharedTask.GetPartnerCopyTaskId(originalTaskId, partnerEmail);
                    if (copyTaskId.HasValue)
                    {
                        StudentTask.Update(copyTaskId.Value, dto.CourseId, dto.Title, dto.Type,
                            dto.EstimatedHours, dto.DueDate, null,
                            dto.AllowSplitting, null, priority, isManualPriority);
                    }
                }

                StudentTask.ScheduleSharedTaskAtCommonTime(originalTaskId, creatorEmail, partnerEmail);
                StudentTask.ScheduleAll(partnerEmail);
            }
        }

        StudentTask.ScheduleAll(email);

        var reloaded = StudentTask.GetById(id);
        return Ok(reloaded != null ? BuildTaskDto(reloaded) : null);
    }

    // Toggles task completion (cascading parent if all siblings done) and returns ML accuracy stats.
    [HttpPost("{id}/complete")]
    public IActionResult Complete(int id, [FromBody] CompleteTaskDto? dto = null)
    {
        var email = GetEmail();
        var task = StudentTask.GetById(id);
        if (task == null || task.Email != email) return NotFound();

        var newIsCompleted = !task.IsCompleted;
        StudentTask.Complete(id, newIsCompleted, newIsCompleted ? dto?.ActualHours : null);

        if (task.ParentTaskId.HasValue && newIsCompleted)
        {
            if (StudentTask.CheckAllSiblingsComplete(task.ParentTaskId.Value))
            {
                StudentTask.Complete(task.ParentTaskId.Value, true);
            }
        }

        StudentTask.ScheduleAll(email);

        // Compute ML stats
        object? mlStats = null;
        if (newIsCompleted && task.CourseId > 0)
        {
            var mlData = StudentTask.GetMLData(email, task.CourseId);
            if (mlData.Any())
            {
                var avgRatio = mlData.Average(t => (double)t.ActualHours / (double)t.EstimatedHours);
                var bias = avgRatio > 1.2 ? "underestimate" : avgRatio < 0.8 ? "overestimate" : "accurate";
                mlStats = new { courseAvgRatio = avgRatio, estimationBias = bias, sampleSize = mlData.Count };
            }
        }

        return Ok(new { task.TaskId, IsCompleted = newIsCompleted, ActualHours = newIsCompleted ? dto?.ActualHours : null, mlStats });
    }

    // Splits a task into the supplied list of subtasks linked by ParentTaskId.
    [HttpPost("{id}/split")]
    public IActionResult Split(int id, [FromBody] SplitTaskDto dto)
    {
        var email = GetEmail();
        var task = StudentTask.GetById(id);
        if (task == null || task.Email != email) return NotFound();
        if (task.ParentTaskId.HasValue) return BadRequest(new { message = "Cannot split a sub-task" });

        foreach (var sub in dto.SubTasks)
        {
            StudentTask.Create(task.CourseId, email, sub.Title, task.Type,
                sub.EstimatedHours, sub.DueDate ?? task.DueDate, task.TaskId, false, null, false);
        }

        StudentTask.ScheduleAll(email);

        var reloaded = StudentTask.GetById(id);
        return Ok(reloaded != null ? BuildTaskDto(reloaded) : null);
    }

    // Deletes a task the user owns and re-runs scheduling.
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var email = GetEmail();
        var task = StudentTask.GetById(id);
        if (task == null || task.Email != email) return NotFound();

        StudentTask.Delete(id);
        StudentTask.ScheduleAll(email);

        return NoContent();
    }

    // Suggests an adjusted hour estimate based on the user's past actual/estimated ratios for the course.
    [HttpGet("suggest-hours")]
    public IActionResult SuggestHours([FromQuery] int courseId, [FromQuery] decimal? estimatedHours)
    {
        var email = GetEmail();
        var mlData = StudentTask.GetMLData(email, courseId);

        if (mlData.Count < 2)
            return Ok(new { hasSuggestion = false });

        var avgRatio = mlData.Average(t => (double)t.ActualHours / (double)t.EstimatedHours);
        var suggested = estimatedHours.HasValue ? Math.Round((double)estimatedHours.Value * avgRatio, 1) : (double?)null;

        return Ok(new
        {
            hasSuggestion = true,
            adjustmentFactor = Math.Round(avgRatio, 2),
            suggestedHours = suggested,
            sampleSize = mlData.Count
        });
    }

    // Returns per-course estimation accuracy stats from completed tasks.
    [HttpGet("learning-insights")]
    public IActionResult GetLearningInsights()
    {
        var email = GetEmail();
        var insights = StudentTask.GetMLInsights(email);

        return Ok(insights.Select(i => new
        {
            courseId = i.CourseId,
            courseName = i.CourseName,
            taskCount = i.TaskCount,
            avgEstimated = Math.Round(i.AvgEstimated, 1),
            avgActual = Math.Round(i.AvgActual, 1),
            accuracy = Math.Round(i.Accuracy, 0)
        }));
    }

    // Builds the wire-format TaskDto including subtasks, sharing, and scheduled slots.
    private TaskDto BuildTaskDto(TaskWithCourse t)
    {
        var taskEvents = StudentTask.GetTaskEvents(t.TaskId);
        var scheduledEvents = taskEvents.Where(te => te.Status == "Scheduled" || te.Status == "Partial").ToList();

        string schedulingStatus;
        if (t.IsCompleted) schedulingStatus = "Completed";
        else if (!scheduledEvents.Any()) schedulingStatus = "Unscheduled";
        else if (scheduledEvents.Any(te => te.Status == "Partial")) schedulingStatus = "Partial";
        else schedulingStatus = "Scheduled";

        var subTasks = StudentTask.GetSubTasks(t.TaskId);
        var completedSubCount = subTasks.Count(s => s.IsCompleted);

        // Shared task info
        var sharedInfo = StudentTask.GetSharedInfo(t.TaskId);
        var isShared = sharedInfo != null;
        string? sharedStatus = sharedInfo?.SharedStatus;
        string? sharedWithName = null;
        string? sharedWithEmail = null;
        if (sharedInfo?.Members != null)
        {
            var otherMember = sharedInfo.Members.FirstOrDefault(m =>
                !string.Equals(m.Email, t.Email, StringComparison.OrdinalIgnoreCase));
            sharedWithName = otherMember?.FullName;
            sharedWithEmail = otherMember?.Email;
        }

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
            ActualHours = t.ActualHours,
            ParentTaskId = t.ParentTaskId,
            SubTasks = subTasks.Any() ? subTasks.Select(s => BuildTaskDto(s)).ToList() : null,
            SubTaskCount = subTasks.Count,
            CompletedSubTaskCount = completedSubCount,
            SubTaskProgress = subTasks.Count > 0 ? Math.Round((double)completedSubCount / subTasks.Count * 100, 0) : 0,
            IsShared = isShared,
            SharedStatus = sharedStatus,
            SharedWithName = sharedWithName,
            SharedWithEmail = sharedWithEmail,
            ScheduledDate = scheduledEvents.OrderBy(te => te.From).FirstOrDefault()?.From,
            SchedulingStatus = schedulingStatus,
            ScheduledSlots = scheduledEvents.Select(te => new TaskSlotDto
            {
                From = te.From,
                To = te.To
            }).OrderBy(s => s.From).ToList(),
            AllowSplitting = t.AllowSplitting,
            IsManuallyPinned = t.IsManuallyPinned,
            IsManualPriority = t.IsManualPriority
        };
    }
}
