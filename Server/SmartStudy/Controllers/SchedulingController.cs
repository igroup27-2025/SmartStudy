using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/scheduling")]
[Authorize]
public class SchedulingController : ControllerBase
{
    private readonly SchedulingService _schedulingService;
    private readonly DBservices _dal;

    public SchedulingController(SchedulingService schedulingService, DBservices dal)
    {
        _schedulingService = schedulingService;
        _dal = dal;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpPost("run")]
    public async Task<IActionResult> RunScheduling()
    {
        var email = GetEmail();
        var result = await _schedulingService.ScheduleAllTasksAsync(email);
        return Ok(result);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var email = GetEmail();
        var result = await _schedulingService.GetSchedulingStatusAsync(email);
        return Ok(result);
    }

    [HttpPost("approve/{taskId}")]
    public async Task<IActionResult> ApproveScheduledTask(int taskId)
    {
        var email = GetEmail();
        var now = DateTime.Now;

        // Verify task exists and belongs to user (case-insensitive — JWT claim
        // and DB email can differ in casing).
        var task = await _dal.GetTaskByIdAsync(taskId);
        if (task == null || !string.Equals(task.Email, email, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        var (approvedCount, removedPast) = await _dal.ApproveTaskEventsAsync(taskId, email, now);

        return Ok(new { message = "Task schedule approved.", approvedCount, removedPast });
    }

}
