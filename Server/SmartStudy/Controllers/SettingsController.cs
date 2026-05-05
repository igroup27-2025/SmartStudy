using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;
using SmartStudy.Services;
using UserModel = SmartStudy.Models.User;
using NotifSettingsModel = SmartStudy.Models.NotificationSettings;
using SchedPrefsModel = SmartStudy.Models.SchedulingPreferences;

namespace SmartStudy.Controllers;

// API endpoints for user profile, notifications, scheduling preferences, onboarding, and integrations.
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly RuppinetSyncService _ruppinetSync;
    private readonly MoodleSyncService _moodleSync;

    // Injects the Ruppinet and Moodle sync services.
    public SettingsController(RuppinetSyncService ruppinetSync, MoodleSyncService moodleSync)
    {
        _ruppinetSync = ruppinetSync;
        _moodleSync = moodleSync;
    }

    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Returns the deployed app version — anonymous health/version probe.
    [AllowAnonymous]
    [HttpGet("version")]
    public IActionResult GetVersion() => Ok(new { version = "2.1-moodle", timestamp = "2026-04-06T18:00:00" });

    // Returns the user's profile and notification settings.
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        var notifSettings = NotifSettingsModel.GetByEmail(email);

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

    // Updates the user's first/last name.
    [HttpPut("profile")]
    public IActionResult UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        UserModel.UpdateProfile(email, dto.FirstName, dto.LastName);

        return Ok(new
        {
            Email = email,
            FirstName = dto.FirstName ?? user.FirstName,
            LastName = dto.LastName ?? user.LastName
        });
    }

    // Upserts notification toggles and quiet-hours window for the user.
    [HttpPut("notifications")]
    public IActionResult UpdateNotifications([FromBody] NotificationSettingsDto dto)
    {
        var email = GetEmail();

        TimeSpan? qStart = null, qEnd = null;
        if (dto.QuietHoursStart != null && TimeSpan.TryParse(dto.QuietHoursStart, out var qs)) qStart = qs;
        if (dto.QuietHoursEnd != null && TimeSpan.TryParse(dto.QuietHoursEnd, out var qe)) qEnd = qe;

        NotifSettingsModel.Upsert(email, dto.NotifyBeforeTask, dto.DailyMorningSummary,
            dto.WeeklyPlanReminder, dto.EnablePushNotification, qStart, qEnd);

        return Ok(dto);
    }

    // Returns the user's auto-scheduler preferences (study caps, sleep, lunch, exam prep).
    [HttpGet("scheduling")]
    public IActionResult GetSchedulingPrefs()
    {
        var email = GetEmail();
        var prefs = SchedPrefsModel.GetByEmail(email);

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

    // Validates and upserts the user's auto-scheduler preferences (each value clamped).
    [HttpPut("scheduling")]
    public IActionResult UpdateSchedulingPrefs([FromBody] SchedulingPreferencesDto dto)
    {
        var email = GetEmail();

        var prefs = new SchedPrefsModel
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

        SchedPrefsModel.Upsert(prefs);
        return Ok(dto);
    }

    // Persists onboarding answers (prefs, notifs, weekly constraints) and marks onboarding complete.
    [HttpPut("onboarding")]
    public IActionResult SaveOnboarding([FromBody] OnboardingDto dto)
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        if (dto.SchedulingPreferences != null)
        {
            var p = dto.SchedulingPreferences;
            var prefs = new SchedPrefsModel
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

            SchedPrefsModel.Upsert(prefs);
        }

        if (dto.NotificationSettings != null)
        {
            var ns = dto.NotificationSettings;
            TimeSpan? qStart = null, qEnd = null;
            if (ns.QuietHoursStart != null && TimeSpan.TryParse(ns.QuietHoursStart, out var qs)) qStart = qs;
            if (ns.QuietHoursEnd != null && TimeSpan.TryParse(ns.QuietHoursEnd, out var qe)) qEnd = qe;

            NotifSettingsModel.Upsert(email, ns.NotifyBeforeTask, ns.DailyMorningSummary,
                ns.WeeklyPlanReminder, ns.EnablePushNotification, qStart, qEnd);
        }

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
                        Event.CreateWork(email, eventFrom, eventTo, true, null, c.Name);
                    else
                        Event.CreatePersonal(email, eventFrom, eventTo, true, null, "Constraint", c.Name);
                }
            }
        }

        UserModel.SetOnboardingComplete(email);

        StudentTask.ScheduleAll(email);

        return Ok(new { message = "Onboarding completed" });
    }

    // Returns the user's Ruppinet connection status and last-sync time.
    [HttpGet("ruppinet")]
    public IActionResult GetRuppinetStatus()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        return Ok(new RuppinetStatusDto
        {
            IsConnected = !string.IsNullOrEmpty(user.RuppinetId),
            RuppinetId = user.RuppinetId,
            LastSync = user.LastRuppinetSync
        });
    }

    // Validates Ruppinet credentials, stores them encrypted, and runs an initial sync.
    [HttpPost("ruppinet/connect")]
    public IActionResult ConnectRuppinet([FromBody] RuppinetConnectDto dto)
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        var valid = _ruppinetSync.TestConnectionAsync(dto.RuppinetId, dto.RuppinetPassword).GetAwaiter().GetResult();
        if (!valid)
            return BadRequest(new { message = "Failed to authenticate with Ruppinet. Check your credentials." });

        UserModel.UpdateRuppinetFields(email, dto.RuppinetId, _ruppinetSync.EncryptPassword(dto.RuppinetPassword));

        var syncResult = _ruppinetSync.SyncAllAsync(email).GetAwaiter().GetResult();

        return Ok(new
        {
            message = "Ruppinet connected successfully",
            syncResult
        });
    }

    // Triggers an on-demand Ruppinet sync for the user.
    [HttpPost("ruppinet/sync")]
    public IActionResult SyncRuppinet()
    {
        var email = GetEmail();
        var result = _ruppinetSync.SyncAllAsync(email).GetAwaiter().GetResult();
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    // Clears the user's stored Ruppinet credentials.
    [HttpDelete("ruppinet")]
    public IActionResult DisconnectRuppinet()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        UserModel.ClearRuppinet(email);

        return Ok(new { message = "Ruppinet disconnected" });
    }

    // ── Moodle Integration ─────────────────────────────────

    // Reports whether Moodle sync is available (requires Ruppinet credentials).
    [HttpGet("moodle")]
    public IActionResult GetMoodleStatus()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        return Ok(new MoodleStatusDto
        {
            IsAvailable = !string.IsNullOrEmpty(user.RuppinetId) && !string.IsNullOrEmpty(user.RuppinetPassword),
            LastSync = user.LastMoodleSync
        });
    }

    // Diagnostic endpoint that returns the raw Moodle fetch payload for the user.
    [HttpGet("moodle/debug")]
    public IActionResult DebugMoodle()
    {
        var email = GetEmail();
        var result = _moodleSync.DebugFetchAsync(email).GetAwaiter().GetResult();
        return Ok(result);
    }

    // Triggers an on-demand Moodle assignment/quiz sync for the user.
    [HttpPost("moodle/sync")]
    public IActionResult SyncMoodle()
    {
        var email = GetEmail();
        var result = _moodleSync.SyncAllAsync(email).GetAwaiter().GetResult();
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    // Clears the user's Moodle linkage data.
    [HttpDelete("moodle")]
    public IActionResult DisconnectMoodle()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);
        if (user == null) return NotFound();

        UserModel.ClearMoodle(email);

        return Ok(new { message = "Moodle disconnected" });
    }

    // Returns the global list of instructors for course-creation dropdowns.
    [HttpGet("instructors")]
    public IActionResult GetInstructors()
    {
        var instructors = Instructor.GetAll();
        return Ok(instructors.Select(i => new { i.InstructorId, i.InstructorName }));
    }
}
