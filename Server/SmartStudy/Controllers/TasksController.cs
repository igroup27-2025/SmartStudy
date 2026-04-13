using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly DBservices _db;
    private readonly SchedulingService _scheduling;

    public TasksController(DBservices db, SchedulingService scheduling)
    {
        _db = db;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId, [FromQuery] bool? completed)
    {
        var email = GetEmail();
        var tasks = await _db.GetTasksByUserAsync(email, courseId, completed);

        var dtos = new List<TaskDto>();
        foreach (var t in tasks)
        {
            dtos.Add(await BuildTaskDtoAsync(t));
        }
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var email = GetEmail();
        var task = await _db.GetTaskByIdAsync(id);
        if (task == null || task.Email != email) return NotFound();

        return Ok(await BuildTaskDtoAsync(task));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskDto dto)
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
            var parent = await _db.GetTaskByIdAsync(dto.ParentTaskId.Value);
            if (parent == null || parent.Email != email)
                return BadRequest(new { message = "Parent task not found" });
            if (courseId == 0) courseId = parent.CourseId;
            if (!dueDate.HasValue) dueDate = parent.DueDate;
        }

        var taskId = await _db.CreateTaskAsync(courseId, email, dto.Title, dto.Type,
            dto.EstimatedHours, dueDate, dto.ParentTaskId, dto.AllowSplitting, priority, isManualPriority);

        string? autoSharedPartner = null;
        bool autoSharedConfirmed = false;

        var userCourse = await _db.GetUserCourseAsync(email, courseId);
        if (userCourse?.SharedByDefault == true && !string.IsNullOrEmpty(userCourse.StudyPartnerEmail))
        {
            // Creator row = Accepted immediately; creator can always run the schedule.
            await _db.CreateSharedTaskAsync(taskId, email, "Pending");
            await _db.CreateSharedTaskMemberAsync(taskId, email, "Accepted", DateTime.UtcNow);

            var partnerUserCourse = await _db.GetUserCourseAsync(userCourse.StudyPartnerEmail, courseId);
            var autoApproved = partnerUserCourse?.CourseShareApproved == true;

            await _db.CreateSharedTaskMemberAsync(taskId, userCourse.StudyPartnerEmail,
                autoApproved ? "Accepted" : "Pending",
                autoApproved ? DateTime.UtcNow : null);

            if (autoApproved)
            {
                await _db.UpdateSharedTaskStatusAsync(taskId, "Confirmed");
                autoSharedConfirmed = true;
                autoSharedPartner = userCourse.StudyPartnerEmail;
            }
        }

        if (autoSharedConfirmed && autoSharedPartner != null)
        {
            // Create the partner's copy task AND place both calendars at a mutual-
            // free slot (NeedReview) BEFORE rescheduling so the rest of each
            // user's tasks slot around the shared block.
            await _scheduling.EnsurePartnerCopyAndScheduleAsync(taskId, email, autoSharedPartner);
            await _scheduling.ScheduleAllTasksAsync(email);
            await _scheduling.ScheduleAllTasksAsync(autoSharedPartner);
        }
        else
        {
            await _scheduling.ScheduleAllTasksAsync(email);
        }

        var reloaded = await _db.GetTaskByIdAsync(taskId);
        return CreatedAtAction(nameof(Get), new { id = taskId }, reloaded != null ? await BuildTaskDtoAsync(reloaded) : null);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        var email = GetEmail();
        var task = await _db.GetTaskByIdAsync(id);
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

        await _db.UpdateTaskAsync(id, dto.CourseId, dto.Title, dto.Type, dto.EstimatedHours,
            dto.DueDate, dto.IsCompleted, dto.AllowSplitting, dto.IsManuallyPinned, priority, isManualPriority);

        await _scheduling.ScheduleAllTasksAsync(email);

        var reloaded = await _db.GetTaskByIdAsync(id);
        return Ok(reloaded != null ? await BuildTaskDtoAsync(reloaded) : null);
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteTaskDto? dto = null)
    {
        var email = GetEmail();
        var task = await _db.GetTaskByIdAsync(id);
        if (task == null || task.Email != email) return NotFound();

        var newIsCompleted = !task.IsCompleted;
        await _db.CompleteTaskAsync(id, newIsCompleted, newIsCompleted ? dto?.ActualHours : null);

        // If this is a sub-task and all siblings are complete, complete parent
        if (task.ParentTaskId.HasValue && newIsCompleted)
        {
            if (await _db.CheckAllSiblingsCompleteAsync(task.ParentTaskId.Value))
            {
                await _db.CompleteTaskAsync(task.ParentTaskId.Value, true);
            }
        }

        await _scheduling.ScheduleAllTasksAsync(email);

        // Compute ML stats
        object? mlStats = null;
        if (newIsCompleted && task.CourseId > 0)
        {
            var mlData = await _db.GetMLDataAsync(email, task.CourseId);
            if (mlData.Any())
            {
                var avgRatio = mlData.Average(t => (double)t.ActualHours / (double)t.EstimatedHours);
                var bias = avgRatio > 1.2 ? "underestimate" : avgRatio < 0.8 ? "overestimate" : "accurate";
                mlStats = new { courseAvgRatio = avgRatio, estimationBias = bias, sampleSize = mlData.Count };
            }
        }

        return Ok(new { task.TaskId, IsCompleted = newIsCompleted, ActualHours = newIsCompleted ? dto?.ActualHours : null, mlStats });
    }

    [HttpPost("{id}/split")]
    public async Task<IActionResult> Split(int id, [FromBody] SplitTaskDto dto)
    {
        var email = GetEmail();
        var task = await _db.GetTaskByIdAsync(id);
        if (task == null || task.Email != email) return NotFound();
        if (task.ParentTaskId.HasValue) return BadRequest(new { message = "Cannot split a sub-task" });

        foreach (var sub in dto.SubTasks)
        {
            await _db.CreateTaskAsync(task.CourseId, email, sub.Title, task.Type,
                sub.EstimatedHours, sub.DueDate ?? task.DueDate, task.TaskId, false, null, false);
        }

        await _scheduling.ScheduleAllTasksAsync(email);

        var reloaded = await _db.GetTaskByIdAsync(id);
        return Ok(reloaded != null ? await BuildTaskDtoAsync(reloaded) : null);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = GetEmail();
        var task = await _db.GetTaskByIdAsync(id);
        if (task == null || task.Email != email) return NotFound();

        await _db.DeleteTaskAsync(id);
        await _scheduling.ScheduleAllTasksAsync(email);

        return NoContent();
    }

    [HttpGet("suggest-hours")]
    public async Task<IActionResult> SuggestHours([FromQuery] int courseId, [FromQuery] decimal? estimatedHours)
    {
        var email = GetEmail();
        var mlData = await _db.GetMLDataAsync(email, courseId);

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

    [HttpGet("learning-insights")]
    public async Task<IActionResult> GetLearningInsights()
    {
        var email = GetEmail();
        var insights = await _db.GetMLInsightsAsync(email);

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

    private async Task<TaskDto> BuildTaskDtoAsync(TaskWithCourse t)
    {
        var taskEvents = await _db.GetTaskEventsAsync(t.TaskId);
        var scheduledEvents = taskEvents.Where(te => te.Status == "Scheduled" || te.Status == "Partial").ToList();

        string schedulingStatus;
        if (t.IsCompleted) schedulingStatus = "Completed";
        else if (!scheduledEvents.Any()) schedulingStatus = "Unscheduled";
        else if (scheduledEvents.Any(te => te.Status == "Partial")) schedulingStatus = "Partial";
        else schedulingStatus = "Scheduled";

        var subTasks = await _db.GetSubTasksAsync(t.TaskId);
        var completedSubCount = subTasks.Count(s => s.IsCompleted);

        // Shared task info
        var sharedInfo = await _db.GetSharedInfoAsync(t.TaskId);
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
            SubTasks = subTasks.Any() ? await Task.WhenAll(subTasks.Select(async s => await BuildTaskDtoAsync(s))).ContinueWith(t => t.Result.ToList()) : null,
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
