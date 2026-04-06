using System.Text.Json;
using SmartStudy.DAL;

namespace SmartStudy.Services;

public class GoogleCalendarService
{
    private readonly DBservices _dal;
    private readonly IConfiguration _config;
    private readonly ComposioService _composio;

    public GoogleCalendarService(DBservices dal, IConfiguration config, ComposioService composio)
    {
        _dal = dal;
        _config = config;
        _composio = composio;
    }

    public bool IsEnabled => _config.GetValue<bool>("Google:CalendarApiEnabled");

    /// <summary>
    /// Sync events through Composio (handles OAuth tokens automatically).
    /// </summary>
    public async Task<CalendarSyncResult> SyncViaComposioAsync(string email, string connectedAccountId, DateTime from, DateTime to)
    {
        var result = new CalendarSyncResult();

        try
        {
            var timeMin = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            var timeMax = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

            var toolResult = await _composio.ExecuteToolAsync(
                "GOOGLECALENDAR_EVENTS_LIST",
                email,
                connectedAccountId,
                new Dictionary<string, object>
                {
                    ["calendarId"] = "primary",
                    ["timeMin"] = timeMin,
                    ["timeMax"] = timeMax,
                    ["singleEvents"] = true,
                    ["orderBy"] = "startTime"
                });

            if (!toolResult.Success)
            {
                result.Message = $"Failed to fetch events from Google Calendar via Composio (HTTP {toolResult.StatusCode})";
                return result;
            }

            var events = ParseComposioEvents(toolResult.RawJson);

            foreach (var evt in events)
            {
                var existing = await _dal.FindPersonalEventByGcalIdAsync(email, $"[gcal:{evt.GoogleId}]");

                if (existing != null)
                {
                    await _dal.UpdateEventTimesAsync(existing.EventId, evt.From, evt.To);
                    result.UpdatedCount++;
                }
                else
                {
                    await _dal.CreatePersonalEventAsync(email, evt.From, evt.To, false, null,
                        "Google Calendar", $"{evt.Summary} [gcal:{evt.GoogleId}]");
                    result.CreatedCount++;
                }
            }

            await _dal.UpdateLastCalendarSyncAsync(email, DateTime.UtcNow);

            result.Success = true;
            result.Message = $"Synced {result.CreatedCount} new, {result.UpdatedCount} updated events";
        }
        catch (Exception ex)
        {
            result.Message = $"Composio sync failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Legacy: sync events using a direct Google access token.
    /// </summary>
    public async Task<CalendarSyncResult> SyncEventsAsync(string email, string accessToken, DateTime from, DateTime to)
    {
        var result = new CalendarSyncResult();

        if (!IsEnabled)
        {
            result.Message = "Google Calendar API is not enabled";
            return result;
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var timeMin = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            var timeMax = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            var url = $"https://www.googleapis.com/calendar/v3/calendars/primary/events?timeMin={timeMin}&timeMax={timeMax}&singleEvents=true&orderBy=startTime";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                result.Message = "Failed to fetch events from Google Calendar";
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                var summary = item.TryGetProperty("summary", out var s) ? s.GetString() : "Google Event";
                var startProp = item.GetProperty("start");
                var endProp = item.GetProperty("end");

                DateTime eventFrom, eventTo;

                if (startProp.TryGetProperty("dateTime", out var dtStart))
                    eventFrom = DateTime.Parse(dtStart.GetString()!);
                else if (startProp.TryGetProperty("date", out var dStart))
                {
                    eventFrom = DateTime.Parse(dStart.GetString()!);
                    eventTo = eventFrom.AddDays(1);
                    continue; // Skip all-day events
                }
                else continue;

                if (endProp.TryGetProperty("dateTime", out var dtEnd))
                    eventTo = DateTime.Parse(dtEnd.GetString()!);
                else
                    eventTo = eventFrom.AddHours(1);

                var googleId = item.GetProperty("id").GetString();

                var existing = await _dal.FindPersonalEventByGcalIdAsync(email, $"[gcal:{googleId}]");

                if (existing != null)
                {
                    await _dal.UpdateEventTimesAsync(existing.EventId, eventFrom, eventTo);
                    result.UpdatedCount++;
                }
                else
                {
                    await _dal.CreatePersonalEventAsync(email, eventFrom, eventTo, false, null,
                        "Google Calendar", $"{summary} [gcal:{googleId}]");
                    result.CreatedCount++;
                }
            }

            await _dal.UpdateGoogleTokenAsync(email, accessToken, DateTime.UtcNow);

            result.Success = true;
            result.Message = $"Synced {result.CreatedCount} new, {result.UpdatedCount} updated events";
        }
        catch (Exception ex)
        {
            result.Message = $"Sync failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Parses event data from Composio's GOOGLECALENDAR_LIST_EVENTS response.
    /// Handles multiple response formats.
    /// </summary>
    private List<ParsedGoogleEvent> ParseComposioEvents(string rawJson)
    {
        var events = new List<ParsedGoogleEvent>();

        try
        {
            var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            JsonElement items;
            bool found = false;

            // Composio wraps results in { data: { ... } } or { response_data: { items: [...] } }
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("items", out items)) found = true;
                else if (data.ValueKind == JsonValueKind.Array) { items = data; found = true; }
                else items = default;
            }
            else if (root.TryGetProperty("response_data", out var respData))
            {
                if (respData.TryGetProperty("items", out items)) found = true;
                else items = default;
            }
            else if (root.TryGetProperty("items", out items))
            {
                found = true;
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                items = root;
                found = true;
            }
            else
            {
                items = default;
            }

            if (!found) return events;

            foreach (var item in items.EnumerateArray())
            {
                var summary = item.TryGetProperty("summary", out var s) ? s.GetString() ?? "Google Event" : "Google Event";
                var googleId = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrEmpty(googleId)) continue;

                DateTime? eventFrom = null, eventTo = null;

                if (item.TryGetProperty("start", out var startProp))
                {
                    if (startProp.TryGetProperty("dateTime", out var dtStart))
                        eventFrom = DateTime.Parse(dtStart.GetString()!);
                    else if (startProp.TryGetProperty("date", out _))
                        continue; // Skip all-day events
                }

                if (item.TryGetProperty("end", out var endProp))
                {
                    if (endProp.TryGetProperty("dateTime", out var dtEnd))
                        eventTo = DateTime.Parse(dtEnd.GetString()!);
                }

                if (eventFrom == null) continue;
                eventTo ??= eventFrom.Value.AddHours(1);

                events.Add(new ParsedGoogleEvent
                {
                    GoogleId = googleId,
                    Summary = summary,
                    From = eventFrom.Value,
                    To = eventTo.Value
                });
            }
        }
        catch
        {
            // Parsing failure — return empty list
        }

        return events;
    }

    private class ParsedGoogleEvent
    {
        public string GoogleId { get; set; } = "";
        public string Summary { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }
}

public class CalendarSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
}
