using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;
using SmartStudy.Models;
using SmartStudy.Services;

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
            settings = new NotificationSettings { Email = email };
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

    [HttpGet("scheduling")]
    public async Task<IActionResult> GetSchedulingPrefs()
    {
        var email = GetEmail();
        var prefs = await _db.SchedulingPreferences.FindAsync(email);

        return Ok(new SchedulingPreferencesDto
        {
            MaxDailyStudyHours = prefs?.MaxDailyStudyHours ?? 6.0,
            MaxContinuousMinutes = prefs?.MaxContinuousMinutes ?? 90,
            DayStartHour = prefs?.DayStartHour ?? 8,
            DayEndHour = prefs?.DayEndHour ?? 22,
            SleepHoursPerDay = prefs?.SleepHoursPerDay ?? 8.0,
            LunchBreakStart = prefs?.LunchBreakStart?.ToString(@"hh\:mm"),
            LunchBreakEnd = prefs?.LunchBreakEnd?.ToString(@"hh\:mm")
        });
    }

    [HttpPut("scheduling")]
    public async Task<IActionResult> UpdateSchedulingPrefs([FromBody] SchedulingPreferencesDto dto)
    {
        var email = GetEmail();
        var prefs = await _db.SchedulingPreferences.FindAsync(email);
        if (prefs == null)
        {
            prefs = new SchedulingPreferences { Email = email };
            _db.SchedulingPreferences.Add(prefs);
        }

        prefs.MaxDailyStudyHours = Math.Clamp(dto.MaxDailyStudyHours, 2, 12);
        prefs.MaxContinuousMinutes = Math.Clamp(dto.MaxContinuousMinutes, 30, 120);
        prefs.DayStartHour = Math.Clamp(dto.DayStartHour, 5, 12);
        prefs.DayEndHour = Math.Clamp(dto.DayEndHour, 16, 23);
        prefs.SleepHoursPerDay = Math.Clamp(dto.SleepHoursPerDay, 5, 10);

        if (dto.LunchBreakStart != null && TimeSpan.TryParse(dto.LunchBreakStart, out var lStart))
            prefs.LunchBreakStart = lStart;
        else
            prefs.LunchBreakStart = null;

        if (dto.LunchBreakEnd != null && TimeSpan.TryParse(dto.LunchBreakEnd, out var lEnd))
            prefs.LunchBreakEnd = lEnd;
        else
            prefs.LunchBreakEnd = null;

        await _db.SaveChangesAsync();
        return Ok(dto);
    }

    [HttpPut("onboarding")]
    public async Task<IActionResult> SaveOnboarding([FromBody] OnboardingDto dto)
    {
        var email = GetEmail();
        var user = await _db.Users.FindAsync(email);
        if (user == null) return NotFound();

        // Save scheduling preferences
        if (dto.SchedulingPreferences != null)
        {
            var p = dto.SchedulingPreferences;
            var prefs = await _db.SchedulingPreferences.FindAsync(email);
            if (prefs == null)
            {
                prefs = new SchedulingPreferences { Email = email };
                _db.SchedulingPreferences.Add(prefs);
            }

            prefs.MaxDailyStudyHours = Math.Clamp(p.MaxDailyStudyHours, 2, 12);
            prefs.MaxContinuousMinutes = Math.Clamp(p.MaxContinuousMinutes, 30, 120);
            prefs.DayStartHour = Math.Clamp(p.DayStartHour, 5, 12);
            prefs.DayEndHour = Math.Clamp(p.DayEndHour, 16, 23);
            prefs.SleepHoursPerDay = Math.Clamp(p.SleepHoursPerDay, 5, 10);

            if (p.LunchBreakStart != null && TimeSpan.TryParse(p.LunchBreakStart, out var lStart))
                prefs.LunchBreakStart = lStart;
            if (p.LunchBreakEnd != null && TimeSpan.TryParse(p.LunchBreakEnd, out var lEnd))
                prefs.LunchBreakEnd = lEnd;
        }

        // Save notification settings
        if (dto.NotificationSettings != null)
        {
            var ns = dto.NotificationSettings;
            var settings = await _db.NotificationSettings.FindAsync(email);
            if (settings == null)
            {
                settings = new NotificationSettings { Email = email };
                _db.NotificationSettings.Add(settings);
            }

            settings.NotifyBeforeTask = ns.NotifyBeforeTask;
            settings.DailyMorningSummary = ns.DailyMorningSummary;
            settings.WeeklyPlanReminder = ns.WeeklyPlanReminder;
            settings.EnablePushNotification = ns.EnablePushNotification;

            if (ns.QuietHoursStart != null && TimeSpan.TryParse(ns.QuietHoursStart, out var qStart))
                settings.QuietHoursStart = qStart;
            if (ns.QuietHoursEnd != null && TimeSpan.TryParse(ns.QuietHoursEnd, out var qEnd))
                settings.QuietHoursEnd = qEnd;
        }

        // Create recurring constraint events
        if (dto.Constraints != null)
        {
            foreach (var c in dto.Constraints)
            {
                if (!TimeSpan.TryParse(c.StartTime, out var cStart)) continue;
                if (!TimeSpan.TryParse(c.EndTime, out var cEnd)) continue;

                foreach (var dayOfWeek in c.Days)
                {
                    // Find the next occurrence of this day of week
                    var today = DateTime.Now.Date;
                    var daysUntil = ((dayOfWeek - (int)today.DayOfWeek) + 7) % 7;
                    var eventDate = today.AddDays(daysUntil);
                    var eventFrom = eventDate.Add(cStart);
                    var eventTo = eventDate.Add(cEnd);

                    if (c.Type == "work")
                    {
                        _db.WorkEvents.Add(new WorkEvent
                        {
                            Email = email,
                            From = eventFrom,
                            To = eventTo,
                            Recurring = true,
                            WorkPlace = c.Name
                        });
                    }
                    else
                    {
                        _db.PersonalEvents.Add(new PersonalEvent
                        {
                            Email = email,
                            From = eventFrom,
                            To = eventTo,
                            Recurring = true,
                            Type = "Constraint",
                            Description = c.Name
                        });
                    }
                }
            }
        }

        user.OnboardingCompleted = true;
        await _db.SaveChangesAsync();

        // Trigger rescheduling
        var scheduler = new SchedulingService(_db);
        await scheduler.ScheduleAllTasksAsync(email);

        return Ok(new { message = "Onboarding completed" });
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
