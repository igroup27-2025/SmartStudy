using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly SmartStudyDbContext _db;

    public SettingsController(SmartStudyDbContext db) => _db = db;

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var email = GetEmail();
        var user = await _db.Users.Include(u => u.NotificationSettings)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return NotFound();

        return Ok(new UserProfileDto
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            NotificationSettings = user.NotificationSettings != null ? new NotificationSettingsDto
            {
                NotifyBeforeTask = user.NotificationSettings.NotifyBeforeTask,
                DailyMorningSummary = user.NotificationSettings.DailyMorningSummary,
                WeeklyPlanReminder = user.NotificationSettings.WeeklyPlanReminder,
                EnablePushNotification = user.NotificationSettings.EnablePushNotification,
                QuietHoursStart = user.NotificationSettings.QuietHoursStart?.ToString(@"hh\:mm"),
                QuietHoursEnd = user.NotificationSettings.QuietHoursEnd?.ToString(@"hh\:mm")
            } : null
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var email = GetEmail();
        var user = await _db.Users.FindAsync(email);
        if (user == null) return NotFound();

        if (dto.FirstName != null) user.FirstName = dto.FirstName;
        if (dto.LastName != null) user.LastName = dto.LastName;

        await _db.SaveChangesAsync();
        return Ok(new { user.Email, user.FirstName, user.LastName });
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] NotificationSettingsDto dto)
    {
        var email = GetEmail();
        var settings = await _db.NotificationSettings.FindAsync(email);
        if (settings == null)
        {
            settings = new Models.NotificationSettings { Email = email };
            _db.NotificationSettings.Add(settings);
        }

        settings.NotifyBeforeTask = dto.NotifyBeforeTask;
        settings.DailyMorningSummary = dto.DailyMorningSummary;
        settings.WeeklyPlanReminder = dto.WeeklyPlanReminder;
        settings.EnablePushNotification = dto.EnablePushNotification;

        if (dto.QuietHoursStart != null && TimeSpan.TryParse(dto.QuietHoursStart, out var qStart))
            settings.QuietHoursStart = qStart;
        else
            settings.QuietHoursStart = null;

        if (dto.QuietHoursEnd != null && TimeSpan.TryParse(dto.QuietHoursEnd, out var qEnd))
            settings.QuietHoursEnd = qEnd;
        else
            settings.QuietHoursEnd = null;

        await _db.SaveChangesAsync();
        return Ok(dto);
    }

    [HttpGet("instructors")]
    public async Task<IActionResult> GetInstructors()
    {
        var instructors = await _db.Instructors
            .Select(i => new { i.InstructorId, i.InstructorName })
            .ToListAsync();
        return Ok(instructors);
    }
}
