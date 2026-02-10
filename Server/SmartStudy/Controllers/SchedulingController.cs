using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/scheduling")]
[Authorize]
public class SchedulingController : ControllerBase
{
    private readonly SchedulingService _schedulingService;

    public SchedulingController(SchedulingService schedulingService)
    {
        _schedulingService = schedulingService;
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
}
