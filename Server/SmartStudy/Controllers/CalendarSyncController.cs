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
    private readonly ComposioService _composio;
    private readonly SchedulingService _scheduling;

    public CalendarSyncController(
        SmartStudyDbContext db,
        GoogleCalendarService googleCalendar,
        ComposioService composio,
        SchedulingService scheduling)
    {
        _db = db;
        _googleCalendar = googleCalendar;
        _composio = composio;
        _scheduling = scheduling;
    }

    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    /// <summary>
    /// Initiates a Composio-hosted Google Calendar OAuth connection.
    /// Returns a redirect URL for the user to authenticate with Google.
    /// </summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectDto? dto)
    {
        if (!_composio.IsEnabled)
            return BadRequest(new { message = "Composio integration is not enabled. Configure Composio settings." });

        var email = GetEmail();

        var callbackUrl = dto?.CallbackUrl
            ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/calendar-sync/callback";

        var result = await _composio.InitiateConnectionAsync(email, callbackUrl);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        if (!string.IsNullOrEmpty(result.ConnectedAccountId))
        {
            var user = await _db.Users.FindAsync(email);
            if (user != null)
            {
                user.ComposioConnectedAccountId = result.ConnectedAccountId;
                await _db.SaveChangesAsync();
            }
        }

        return Ok(new { redirectUrl = result.RedirectUrl, connectionId = result.ConnectedAccountId });
    }

    /// <summary>
    /// Callback endpoint that Composio redirects to after OAuth.
    /// Saves the connected account ID and redirects the user back to Settings.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? status,
        [FromQuery] string? connected_account_id,
        [FromQuery] string? connectedAccountId)
    {
        var accountId = connected_account_id ?? connectedAccountId;
        var isSuccess = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        if (isSuccess && !string.IsNullOrEmpty(accountId))
        {
            var account = await _composio.GetConnectedAccountAsync(accountId);
            if (account != null)
            {
                var users = await _db.Users
                    .Where(u => u.ComposioConnectedAccountId == accountId)
                    .ToListAsync();

                foreach (var user in users)
                {
                    user.LastCalendarSync = null;
                    await _db.SaveChangesAsync();
                }
            }
        }

        var redirectPath = $"{Request.PathBase}/Pages/Settings.html?gcal={status ?? "unknown"}";
        return Redirect(redirectPath);
    }

    /// <summary>
    /// Syncs Google Calendar events via Composio.
    /// Falls back to the legacy direct Google API flow if Composio is not configured.
    /// </summary>
    [HttpPost("google")]
    public async Task<IActionResult> SyncGoogle([FromBody] GoogleSyncDto? dto)
    {
        var email = GetEmail();

        if (_composio.IsEnabled)
        {
            var user = await _db.Users.FindAsync(email);
            var connAccountId = user?.ComposioConnectedAccountId;

            if (string.IsNullOrEmpty(connAccountId))
                return BadRequest(new { message = "Google Calendar is not connected. Please connect first." });

            var account = await _composio.GetConnectedAccountAsync(connAccountId);
            if (account == null || account.Status != "ACTIVE")
                return BadRequest(new { message = "Google Calendar connection is not active. Please reconnect." });

            var from = DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow.AddDays(60);

            var result = await _googleCalendar.SyncViaComposioAsync(email, connAccountId, from, to);

            if (result.Success)
                await _scheduling.ScheduleAllTasksAsync(email);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // Legacy flow: direct Google API
        if (!_googleCalendar.IsEnabled)
            return BadRequest(new { message = "Google Calendar sync is not enabled." });

        if (dto == null || string.IsNullOrEmpty(dto.AccessToken))
            return BadRequest(new { message = "Access token is required." });

        var legacyFrom = DateTime.Now.AddDays(-7);
        var legacyTo = DateTime.Now.AddDays(60);

        var legacyResult = await _googleCalendar.SyncEventsAsync(email, dto.AccessToken, legacyFrom, legacyTo);

        if (legacyResult.Success)
            await _scheduling.ScheduleAllTasksAsync(email);

        return legacyResult.Success ? Ok(legacyResult) : BadRequest(legacyResult);
    }

    /// <summary>
    /// Returns the current sync status and connection info.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var email = GetEmail();
        var user = await _db.Users.FindAsync(email);

        var syncedCount = await _db.PersonalEvents
            .CountAsync(pe => pe.Email == email && pe.Description != null && pe.Description.Contains("[gcal:"));

        bool isConnected = false;
        string connectionStatus = "none";

        if (_composio.IsEnabled && !string.IsNullOrEmpty(user?.ComposioConnectedAccountId))
        {
            var account = await _composio.GetConnectedAccountAsync(user.ComposioConnectedAccountId);
            if (account != null)
            {
                connectionStatus = account.Status;
                isConnected = account.Status == "ACTIVE";
            }
        }
        else if (_googleCalendar.IsEnabled && user?.GoogleCalendarAccessToken != null)
        {
            isConnected = true;
            connectionStatus = "ACTIVE";
        }

        return Ok(new
        {
            lastSync = user?.LastCalendarSync,
            syncedEventCount = syncedCount,
            isEnabled = _composio.IsEnabled || _googleCalendar.IsEnabled,
            isConnected,
            connectionStatus,
            useComposio = _composio.IsEnabled
        });
    }

    /// <summary>
    /// Disconnects the Google Calendar Composio account.
    /// </summary>
    [HttpDelete("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        var email = GetEmail();
        var user = await _db.Users.FindAsync(email);

        if (user == null)
            return NotFound();

        if (_composio.IsEnabled && !string.IsNullOrEmpty(user.ComposioConnectedAccountId))
        {
            await _composio.DeleteConnectedAccountAsync(user.ComposioConnectedAccountId);
            user.ComposioConnectedAccountId = null;
        }

        user.GoogleCalendarAccessToken = null;
        user.GoogleCalendarRefreshToken = null;
        user.LastCalendarSync = null;

        // Remove synced events
        var syncedEvents = await _db.PersonalEvents
            .Where(pe => pe.Email == email && pe.Description != null && pe.Description.Contains("[gcal:"))
            .ToListAsync();
        _db.PersonalEvents.RemoveRange(syncedEvents);

        await _db.SaveChangesAsync();

        return Ok(new { message = "Google Calendar disconnected", removedEvents = syncedEvents.Count });
    }
}

public class GoogleSyncDto
{
    public string? AccessToken { get; set; }
}

public class ConnectDto
{
    public string? CallbackUrl { get; set; }
}
