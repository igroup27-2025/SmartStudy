using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/scheduling")]
[Authorize]
public class SchedulingController : ControllerBase
{
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpPost("run")]
    public IActionResult RunScheduling()
    {
        var email = GetEmail();
        var result = StudentTask.ScheduleAll(email);
        return Ok(result);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var email = GetEmail();
        var result = StudentTask.GetSchedulingStatus(email);
        return Ok(result);
    }

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
