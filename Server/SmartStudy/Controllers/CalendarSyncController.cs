using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Models;
using SmartStudy.Services;
using UserModel = SmartStudy.Models.User;

namespace SmartStudy.Controllers;

// API endpoints for connecting and syncing Google Calendar via Composio or direct OAuth.
[ApiController]
[Route("api/calendar-sync")]
[Authorize]
public class CalendarSyncController : ControllerBase
{
    private readonly GoogleCalendarService _googleCalendar;
    private readonly ComposioService _composio;

    // Injects the Google Calendar and Composio integration services.
    public CalendarSyncController(
        GoogleCalendarService googleCalendar,
        ComposioService composio)
    {
        _googleCalendar = googleCalendar;
        _composio = composio;
    }

    // Reads the authenticated user's email from JWT claims.
    private string GetEmail() => User.FindFirst(ClaimTypes.Email)!.Value;

    // Initiates a Composio OAuth flow and returns the redirect URL for the user.
    [HttpPost("connect")]
    public IActionResult Connect([FromBody] ConnectDto? dto)
    {
        if (!_composio.IsEnabled)
            return BadRequest(new { message = "Composio integration is not enabled. Configure Composio settings." });

        var email = GetEmail();

        var callbackUrl = dto?.CallbackUrl
            ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/calendar-sync/callback";

        var result = _composio.InitiateConnectionAsync(email, callbackUrl).GetAwaiter().GetResult();

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        if (!string.IsNullOrEmpty(result.ConnectedAccountId))
        {
            UserModel.UpdateComposioId(email, result.ConnectedAccountId);
        }

        return Ok(new { redirectUrl = result.RedirectUrl, connectionId = result.ConnectedAccountId });
    }

    // OAuth callback — clears LastCalendarSync on success and redirects back to Settings.
    [HttpGet("callback")]
    [AllowAnonymous]
    public IActionResult Callback(
        [FromQuery] string? status,
        [FromQuery] string? connected_account_id,
        [FromQuery] string? connectedAccountId)
    {
        var accountId = connected_account_id ?? connectedAccountId;
        var isSuccess = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        if (isSuccess && !string.IsNullOrEmpty(accountId))
        {
            var account = _composio.GetConnectedAccountAsync(accountId).GetAwaiter().GetResult();
            if (account != null)
            {
                var userEmails = UserModel.GetUsersByComposioId(accountId);
                foreach (var userEmail in userEmails)
                {
                    UserModel.ClearLastCalendarSync(userEmail);
                }
            }
        }

        var frontendBase = Request.PathBase.ToString().Replace("/tar1", "/tar2");
        var redirectPath = $"{frontendBase}/Pages/Settings.html?gcal={status ?? "unknown"}";
        return Redirect(redirectPath);
    }

    // Syncs Google Calendar events into the user's calendar and re-runs auto-scheduling.
    [HttpPost("google")]
    public IActionResult SyncGoogle([FromBody] GoogleSyncDto? dto)
    {
        var email = GetEmail();

        if (_composio.IsEnabled)
        {
            var user = UserModel.GetByEmail(email);
            var connAccountId = user?.ComposioConnectedAccountId;

            if (string.IsNullOrEmpty(connAccountId))
                return BadRequest(new { message = "Google Calendar is not connected. Please connect first." });

            var account = _composio.GetConnectedAccountAsync(connAccountId).GetAwaiter().GetResult();
            if (account == null || account.Status != "ACTIVE")
                return BadRequest(new { message = "Google Calendar connection is not active. Please reconnect." });

            var from = DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow.AddDays(60);

            var result = _googleCalendar.SyncViaComposioAsync(email, connAccountId, from, to).GetAwaiter().GetResult();

            if (result.Success)
                StudentTask.ScheduleAll(email);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        if (!_googleCalendar.IsEnabled)
            return BadRequest(new { message = "Google Calendar sync is not enabled." });

        if (dto == null || string.IsNullOrEmpty(dto.AccessToken))
            return BadRequest(new { message = "Access token is required." });

        var legacyFrom = DateTime.Now.AddDays(-7);
        var legacyTo = DateTime.Now.AddDays(60);

        var legacyResult = _googleCalendar.SyncEventsAsync(email, dto.AccessToken, legacyFrom, legacyTo).GetAwaiter().GetResult();

        if (legacyResult.Success)
            StudentTask.ScheduleAll(email);

        return legacyResult.Success ? Ok(legacyResult) : BadRequest(legacyResult);
    }

    // Returns Google Calendar connection state, last-sync time, and synced event count.
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);

        var syncedCount = UserModel.CountGcalEvents(email);

        bool isConnected = false;
        string connectionStatus = "none";

        if (_composio.IsEnabled && !string.IsNullOrEmpty(user?.ComposioConnectedAccountId))
        {
            var account = _composio.GetConnectedAccountAsync(user.ComposioConnectedAccountId).GetAwaiter().GetResult();
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

    // Disconnects Google Calendar, removes the Composio link, and deletes synced events.
    [HttpDelete("disconnect")]
    public IActionResult Disconnect()
    {
        var email = GetEmail();
        var user = UserModel.GetByEmail(email);

        if (user == null)
            return NotFound();

        if (_composio.IsEnabled && !string.IsNullOrEmpty(user.ComposioConnectedAccountId))
        {
            _composio.DeleteConnectedAccountAsync(user.ComposioConnectedAccountId).GetAwaiter().GetResult();
        }

        var gcalEventIds = UserModel.GetGcalPersonalEventIds(email);
        foreach (var eventId in gcalEventIds)
            UserModel.DeleteEventById(eventId);

        UserModel.DisconnectGoogleCalendar(email);

        return Ok(new { message = "Google Calendar disconnected", removedEvents = gcalEventIds.Count });
    }
}

// Body for legacy direct Google Calendar sync — carries the OAuth access token.
public class GoogleSyncDto
{
    public string? AccessToken { get; set; }
}

// Body for initiating a Composio connection — overrides the default callback URL.
public class ConnectDto
{
    public string? CallbackUrl { get; set; }
}
