using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.DAL;
using SmartStudy.DTOs;
using SmartStudy.Models;
using SmartStudy.Services;

namespace SmartStudy.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly DBservices _db;
    private readonly RuppinetSyncService _ruppinetSync;
    private readonly MoodleSyncService _moodleSync;

    public SettingsController(DBservices db, RuppinetSyncService ruppinetSync, MoodleSyncService moodleSync)
    {
        _db = db;
        _ruppinetSync = ruppinetSync;
        _moodleSync = moodleSync;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    [AllowAnonymous]
    [HttpGet("version")]
    public IActionResult GetVersion() => Ok(new { version = "2.1-moodle", timestamp = "2026-04-06T18:00:00" });

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        var notifSettings = await _db.GetNotifSettingsByEmailAsync(email);

        return Ok(new UserProfileDto
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            NotificationSettings = notifSettings != null ? new NotificationSettingsDto
            {
                NotifyBeforeTask = notifSettings.NotifyBeforeTask,
                DailyMorningSummary = notifSettings.DailyMorningSummary,
                WeeklyPlanReminder = notifSettings.WeeklyPlanReminder,
                EnablePushNotification = notifSettings.EnablePushNotification,
                QuietHoursStart = notifSettings.QuietHoursStart?.ToString(@"hh\:mm"),
                QuietHoursEnd = notifSettings.QuietHoursEnd?.ToString(@"hh\:mm")
            } : null
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        await _db.UpdateUserProfileAsync(email, dto.FirstName, dto.LastName);

        return Ok(new
        {
            Email = email,
            FirstName = dto.FirstName ?? user.FirstName,
            LastName = dto.LastName ?? user.LastName
        });
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] NotificationSettingsDto dto)
    {
        var email = GetEmail();

        TimeSpan? qStart = null, qEnd = null;
        if (dto.QuietHoursStart != null && TimeSpan.TryParse(dto.QuietHoursStart, out var qs)) qStart = qs;
        if (dto.QuietHoursEnd != null && TimeSpan.TryParse(dto.QuietHoursEnd, out var qe)) qEnd = qe;

        await _db.UpsertNotifSettingsAsync(email, dto.NotifyBeforeTask, dto.DailyMorningSummary,
            dto.WeeklyPlanReminder, dto.EnablePushNotification, qStart, qEnd);

        return Ok(dto);
    }

    [HttpGet("scheduling")]
    public async Task<IActionResult> GetSchedulingPrefs()
    {
        var email = GetEmail();
        var prefs = await _db.GetSchedPrefsByEmailAsync(email);

        return Ok(new SchedulingPreferencesDto
        {
            MaxDailyStudyHours = prefs?.MaxDailyStudyHours ?? 6.0,
            MaxContinuousMinutes = prefs?.MaxContinuousMinutes ?? 90,
            DayStartHour = prefs?.DayStartHour ?? 8,
            DayEndHour = prefs?.DayEndHour ?? 22,
            SleepHoursPerDay = prefs?.SleepHoursPerDay ?? 8.0,
            LunchBreakStart = prefs?.LunchBreakStart?.ToString(@"hh\:mm"),
            LunchBreakEnd = prefs?.LunchBreakEnd?.ToString(@"hh\:mm"),
            BreakDurationMinutes = prefs?.BreakDurationMinutes ?? 15,
            DefaultTaskEstimatedHours = prefs?.DefaultTaskEstimatedHours ?? 4.0,
            MaxDailyTotalHours = prefs?.MaxDailyTotalHours ?? 14.0,
            ExamPrepHoursPerDay = prefs?.ExamPrepHoursPerDay ?? 5.0,
            ExamPrepDays = prefs?.ExamPrepDays ?? 3
        });
    }

    [HttpPut("scheduling")]
    public async Task<IActionResult> UpdateSchedulingPrefs([FromBody] SchedulingPreferencesDto dto)
    {
        var email = GetEmail();

        var prefs = new SchedulingPreferences
        {
            Email = email,
            MaxDailyStudyHours = Math.Clamp(dto.MaxDailyStudyHours, 2, 12),
            MaxContinuousMinutes = Math.Clamp(dto.MaxContinuousMinutes, 30, 120),
            DayStartHour = Math.Clamp(dto.DayStartHour, 5, 12),
            DayEndHour = Math.Clamp(dto.DayEndHour, 16, 23),
            SleepHoursPerDay = Math.Clamp(dto.SleepHoursPerDay, 5, 10),
            BreakDurationMinutes = Math.Clamp(dto.BreakDurationMinutes, 5, 60),
            DefaultTaskEstimatedHours = Math.Clamp(dto.DefaultTaskEstimatedHours, 0.5, 20),
            MaxDailyTotalHours = Math.Clamp(dto.MaxDailyTotalHours, 6, 20),
            ExamPrepHoursPerDay = Math.Clamp(dto.ExamPrepHoursPerDay, 1, 12),
            ExamPrepDays = Math.Clamp(dto.ExamPrepDays, 1, 14)
        };

        if (dto.LunchBreakStart != null && TimeSpan.TryParse(dto.LunchBreakStart, out var lStart))
            prefs.LunchBreakStart = lStart;
        if (dto.LunchBreakEnd != null && TimeSpan.TryParse(dto.LunchBreakEnd, out var lEnd))
            prefs.LunchBreakEnd = lEnd;

        await _db.UpsertSchedPrefsAsync(prefs);
        return Ok(dto);
    }

    [HttpPut("onboarding")]
    public async Task<IActionResult> SaveOnboarding([FromBody] OnboardingDto dto)
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        // Save scheduling preferences
        if (dto.SchedulingPreferences != null)
        {
            var p = dto.SchedulingPreferences;
            var prefs = new SchedulingPreferences
            {
                Email = email,
                MaxDailyStudyHours = Math.Clamp(p.MaxDailyStudyHours, 2, 12),
                MaxContinuousMinutes = Math.Clamp(p.MaxContinuousMinutes, 30, 120),
                DayStartHour = Math.Clamp(p.DayStartHour, 5, 12),
                DayEndHour = Math.Clamp(p.DayEndHour, 16, 23),
                SleepHoursPerDay = Math.Clamp(p.SleepHoursPerDay, 5, 10),
                BreakDurationMinutes = Math.Clamp(p.BreakDurationMinutes, 5, 60),
                DefaultTaskEstimatedHours = Math.Clamp(p.DefaultTaskEstimatedHours, 0.5, 20),
                MaxDailyTotalHours = Math.Clamp(p.MaxDailyTotalHours, 6, 20),
                ExamPrepHoursPerDay = Math.Clamp(p.ExamPrepHoursPerDay, 1, 12),
                ExamPrepDays = Math.Clamp(p.ExamPrepDays, 1, 14)
            };
            if (p.LunchBreakStart != null && TimeSpan.TryParse(p.LunchBreakStart, out var lStart))
                prefs.LunchBreakStart = lStart;
            if (p.LunchBreakEnd != null && TimeSpan.TryParse(p.LunchBreakEnd, out var lEnd))
                prefs.LunchBreakEnd = lEnd;

            await _db.UpsertSchedPrefsAsync(prefs);
        }

        // Save notification settings
        if (dto.NotificationSettings != null)
        {
            var ns = dto.NotificationSettings;
            TimeSpan? qStart = null, qEnd = null;
            if (ns.QuietHoursStart != null && TimeSpan.TryParse(ns.QuietHoursStart, out var qs)) qStart = qs;
            if (ns.QuietHoursEnd != null && TimeSpan.TryParse(ns.QuietHoursEnd, out var qe)) qEnd = qe;

            await _db.UpsertNotifSettingsAsync(email, ns.NotifyBeforeTask, ns.DailyMorningSummary,
                ns.WeeklyPlanReminder, ns.EnablePushNotification, qStart, qEnd);
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
                    var today = DateTime.Now.Date;
                    var daysUntil = ((dayOfWeek - (int)today.DayOfWeek) + 7) % 7;
                    var eventDate = today.AddDays(daysUntil);
                    var eventFrom = eventDate.Add(cStart);
                    var eventTo = eventDate.Add(cEnd);

                    if (c.Type == "work")
                        await _db.CreateWorkEventAsync(email, eventFrom, eventTo, true, null, c.Name);
                    else
                        await _db.CreatePersonalEventAsync(email, eventFrom, eventTo, true, null, "Constraint", c.Name);
                }
            }
        }

        await _db.SetOnboardingCompleteAsync(email);

        // Trigger rescheduling
        var scheduler = HttpContext.RequestServices.GetRequiredService<SchedulingService>();
        await scheduler.ScheduleAllTasksAsync(email);

        return Ok(new { message = "Onboarding completed" });
    }

    [HttpGet("ruppinet")]
    public async Task<IActionResult> GetRuppinetStatus()
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        return Ok(new DTOs.RuppinetStatusDto
        {
            IsConnected = !string.IsNullOrEmpty(user.RuppinetId),
            RuppinetId = user.RuppinetId,
            LastSync = user.LastRuppinetSync
        });
    }

    [HttpPost("ruppinet/connect")]
    public async Task<IActionResult> ConnectRuppinet([FromBody] DTOs.RuppinetConnectDto dto)
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        var valid = await _ruppinetSync.TestConnectionAsync(dto.RuppinetId, dto.RuppinetPassword);
        if (!valid)
            return BadRequest(new { message = "Failed to authenticate with Ruppinet. Check your credentials." });

        await _db.UpdateRuppinetFieldsAsync(email, dto.RuppinetId, _ruppinetSync.EncryptPassword(dto.RuppinetPassword));

        var syncResult = await _ruppinetSync.SyncAllAsync(email);

        return Ok(new
        {
            message = "Ruppinet connected successfully",
            syncResult
        });
    }

    [HttpPost("ruppinet/sync")]
    public async Task<IActionResult> SyncRuppinet()
    {
        var email = GetEmail();
        var result = await _ruppinetSync.SyncAllAsync(email);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("ruppinet")]
    public async Task<IActionResult> DisconnectRuppinet()
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        await _db.ClearRuppinetAsync(email);

        return Ok(new { message = "Ruppinet disconnected" });
    }

    // ── Moodle Integration ─────────────────────────────────

    [HttpGet("moodle")]
    public async Task<IActionResult> GetMoodleStatus()
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        return Ok(new DTOs.MoodleStatusDto
        {
            IsAvailable = !string.IsNullOrEmpty(user.RuppinetId) && !string.IsNullOrEmpty(user.RuppinetPassword),
            LastSync = user.LastMoodleSync
        });
    }

    [HttpGet("moodle/debug")]
    public async Task<IActionResult> DebugMoodle()
    {
        var email = GetEmail();
        var result = await _moodleSync.DebugFetchAsync(email);
        return Ok(result);
    }

    [HttpPost("moodle/sync")]
    public async Task<IActionResult> SyncMoodle()
    {
        var email = GetEmail();
        var result = await _moodleSync.SyncAllAsync(email);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("moodle")]
    public async Task<IActionResult> DisconnectMoodle()
    {
        var email = GetEmail();
        var user = await _db.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        await _db.ClearMoodleAsync(email);

        return Ok(new { message = "Moodle disconnected" });
    }

    [HttpGet("instructors")]
    public async Task<IActionResult> GetInstructors()
    {
        var instructors = await _db.GetAllInstructorsAsync();
        return Ok(instructors.Select(i => new { i.InstructorId, i.InstructorName }));
    }
}
