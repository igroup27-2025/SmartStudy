using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/scheduling")]
[Authorize]
public class SchedulingController : ControllerBase
{
    private readonly SchedulingService _schedulingService;
    private readonly SmartStudyDbContext _db;

    public SchedulingController(SchedulingService schedulingService, SmartStudyDbContext db)
    {
        _schedulingService = schedulingService;
        _db = db;
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

        var task = await _db.Tasks
            .FirstOrDefaultAsync(t => t.TaskId == taskId && t.Email == email);
        if (task == null) return NotFound();

        // Change NeedReview events to Scheduled
        var reviewEvents = await _db.TaskEvents
            .Where(te => te.TaskId == taskId && te.Status == "NeedReview")
            .ToListAsync();

        foreach (var ev in reviewEvents)
            ev.Status = "Scheduled";

        // Remove past events (missed study sessions) so task leaves the review list
        var pastEvents = await _db.TaskEvents
            .Where(te => te.TaskId == taskId && te.From < now && te.Status != "NeedReview")
            .ToListAsync();

        _db.TaskEvents.RemoveRange(pastEvents);

        await _db.SaveChangesAsync();
        return Ok(new { message = "Task schedule approved.", approvedCount = reviewEvents.Count, removedPast = pastEvents.Count });
    }

}
