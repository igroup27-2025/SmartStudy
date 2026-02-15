using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.Models;

namespace SmartStudy.Services;

public class GoogleCalendarService
{
    private readonly SmartStudyDbContext _db;
    private readonly IConfiguration _config;

    public GoogleCalendarService(SmartStudyDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public bool IsEnabled => _config.GetValue<bool>("Google:CalendarApiEnabled");

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
            // Use Google Calendar API to fetch events
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
            var doc = System.Text.Json.JsonDocument.Parse(json);
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

                // Check if already imported (by matching description source tag)
                var existing = await _db.PersonalEvents
                    .FirstOrDefaultAsync(pe => pe.Email == email && pe.Description != null &&
                                               pe.Description.Contains($"[gcal:{googleId}]"));

                if (existing != null)
                {
                    existing.From = eventFrom;
                    existing.To = eventTo;
                    result.UpdatedCount++;
                }
                else
                {
                    var evt = new PersonalEvent
                    {
                        Email = email,
                        From = eventFrom,
                        To = eventTo,
                        Recurring = false,
                        Type = "Google Calendar",
                        Description = $"{summary} [gcal:{googleId}]"
                    };
                    _db.PersonalEvents.Add(evt);
                    result.CreatedCount++;
                }
            }

            await _db.SaveChangesAsync();

            // Update user's last sync time
            var user = await _db.Users.FindAsync(email);
            if (user != null)
            {
                user.GoogleCalendarAccessToken = accessToken;
                user.LastCalendarSync = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            result.Success = true;
            result.Message = $"Synced {result.CreatedCount} new, {result.UpdatedCount} updated events";
        }
        catch (Exception ex)
        {
            result.Message = $"Sync failed: {ex.Message}";
        }

        return result;
    }
}

public class CalendarSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
}
