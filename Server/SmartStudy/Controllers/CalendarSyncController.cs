using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/calendar-sync")]
[Authorize]
public class CalendarSyncController : ControllerBase
{
    private readonly SmartStudyDbContext _db;
    private readonly GoogleCalendarService _googleCalendar;
    private readonly SchedulingService _scheduling;

    public CalendarSyncController(SmartStudyDbContext db, GoogleCalendarService googleCalendar, SchedulingService scheduling)
    {
        _db = db;
        _googleCalendar = googleCalendar;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpPost("google")]
    public async Task<IActionResult> SyncGoogle([FromBody] GoogleSyncDto dto)
    {
        if (!_googleCalendar.IsEnabled)
            return BadRequest(new { message = "Google Calendar sync is not enabled. Configure Google:CalendarApiEnabled in settings." });

        var email = GetEmail();
        var from = DateTime.Now.AddDays(-7);
        var to = DateTime.Now.AddDays(60);

        var result = await _googleCalendar.SyncEventsAsync(email, dto.AccessToken, from, to);

        if (result.Success)
        {
            // Trigger workload recalculation
            await _scheduling.ScheduleAllTasksAsync(email);
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var email = GetEmail();
        var user = await _db.Users.FindAsync(email);

        var syncedCount = await _db.PersonalEvents
            .CountAsync(pe => pe.Email == email && pe.Description != null && pe.Description.Contains("[gcal:"));

        return Ok(new
        {
            lastSync = user?.LastCalendarSync,
            syncedEventCount = syncedCount,
            isEnabled = _googleCalendar.IsEnabled
        });
    }
}

public class GoogleSyncDto
{
    public string AccessToken { get; set; } = null!;
}
