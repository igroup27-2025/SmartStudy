using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

// API endpoints for the auto-scheduling engine: run, status, approve.
[ApiController]
[Route("api/scheduling")]
[Authorize]
public class SchedulingController : ControllerBase
{
    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Re-runs the greedy auto-scheduler for the user's incomplete tasks.
    [HttpPost("run")]
    public IActionResult RunScheduling()
    {
        var email = GetEmail();
        var result = StudentTask.ScheduleAll(email);
        return Ok(result);
    }

    // Returns daily workload, overloaded days, and relocation suggestions.
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var email = GetEmail();
        var result = StudentTask.GetSchedulingStatus(email);
        return Ok(result);
    }

    // Marks a task's scheduled study blocks as approved and prunes past slots.
    [HttpPost("approve/{taskId}")]
    public IActionResult ApproveScheduledTask(int taskId)
    {
        var email = GetEmail();
        var now = DateTime.Now;

        var task = StudentTask.GetById(taskId);
        if (task == null || !string.Equals(task.Email, email, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        var (approvedCount, removedPast) = StudentTask.ApproveTaskEvents(taskId, email, now);

        return Ok(new { message = "Task schedule approved.", approvedCount, removedPast });
    }
}
