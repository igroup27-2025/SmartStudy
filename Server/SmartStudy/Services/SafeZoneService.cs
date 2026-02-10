using Microsoft.EntityFrameworkCore;
using SmartStudy.Data;
using SmartStudy.DTOs;

namespace SmartStudy.Services;

public class SafeZoneService
{
    private readonly SmartStudyDbContext _db;
    private readonly StressService _stress;

    public SafeZoneService(SmartStudyDbContext db, StressService stress)
    {
        _db = db;
        _stress = stress;
    }

    public async Task<List<SafeZoneDto>> GetSafeZonesAsync(string myEmail, string friendEmail)
    {
        var now = DateTime.Now;
        var startDate = now.Date;
        var endDate = startDate.AddDays(7);

        // Get stress scores for both users
        var myStress = await _stress.GetStressScoreAsync(myEmail);
        var friendStress = await _stress.GetStressScoreAsync(friendEmail);

        // Get all events for both users in the next 7 days
        var myEvents = await _db.Events
            .Where(e => e.Email == myEmail && e.From < endDate && e.To > startDate)
            .OrderBy(e => e.From)
            .ToListAsync();

        var friendEvents = await _db.Events
            .Where(e => e.Email == friendEmail && e.From < endDate && e.To > startDate)
            .OrderBy(e => e.From)
            .ToListAsync();

        var safeZones = new List<SafeZoneDto>();

        // Scan each day in 30-minute slots (8 AM - 10 PM)
        for (var day = startDate; day < endDate; day = day.AddDays(1))
        {
            var dayStart = day.AddHours(8);
            var dayEnd = day.AddHours(22);

            // Skip past slots for today
            if (day == now.Date && now > dayStart)
            {
                var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour,
                    now.Minute >= 30 ? 30 : 0, 0).AddMinutes(30);
                if (rounded > dayStart) dayStart = rounded;
            }

            if (dayStart >= dayEnd) continue;

            // Build busy intervals for both users on this day
            var myBusy = GetBusyIntervals(myEvents, dayStart, dayEnd);
            var friendBusy = GetBusyIntervals(friendEvents, dayStart, dayEnd);

            // Find free slots for both users using 30-minute grid
            var mutualFree = FindMutualFreeSlots(dayStart, dayEnd, myBusy, friendBusy);

            // Merge adjacent 30-min slots into longer blocks
            var merged = MergeSlots(mutualFree);

            // Filter: only include slots where both stress < 60%
            // Use day-level stress approximation
            foreach (var slot in merged)
            {
                if (slot.duration < TimeSpan.FromMinutes(30)) continue;

                // Both users must have overall stress < 60%
                if (myStress.Score >= 60 && friendStress.Score >= 60) continue;

                safeZones.Add(new SafeZoneDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Day = day.ToString("dddd"),
                    StartTime = slot.from.ToString("HH:mm"),
                    EndTime = slot.to.ToString("HH:mm"),
                    MyStress = myStress.Score,
                    FriendStress = friendStress.Score
                });
            }
        }

        return safeZones;
    }

    private static List<(DateTime from, DateTime to)> GetBusyIntervals(
        List<Models.Event> events, DateTime dayStart, DateTime dayEnd)
    {
        var busy = new List<(DateTime from, DateTime to)>();
        foreach (var e in events)
        {
            if (e.From < dayEnd && e.To > dayStart)
            {
                var from = e.From < dayStart ? dayStart : e.From;
                var to = e.To > dayEnd ? dayEnd : e.To;
                if (from < to) busy.Add((from, to));
            }
        }

        // Sort and merge overlapping
        busy = busy.OrderBy(b => b.from).ToList();
        var merged = new List<(DateTime from, DateTime to)>();
        foreach (var b in busy)
        {
            if (merged.Count > 0 && b.from <= merged[^1].to)
            {
                var last = merged[^1];
                merged[^1] = (last.from, b.to > last.to ? b.to : last.to);
            }
            else
            {
                merged.Add(b);
            }
        }
        return merged;
    }

    private static List<(DateTime from, DateTime to)> FindMutualFreeSlots(
        DateTime dayStart, DateTime dayEnd,
        List<(DateTime from, DateTime to)> myBusy,
        List<(DateTime from, DateTime to)> friendBusy)
    {
        // Combine all busy intervals
        var allBusy = myBusy.Concat(friendBusy).OrderBy(b => b.from).ToList();
        var merged = new List<(DateTime from, DateTime to)>();
        foreach (var b in allBusy)
        {
            if (merged.Count > 0 && b.from <= merged[^1].to)
            {
                var last = merged[^1];
                merged[^1] = (last.from, b.to > last.to ? b.to : last.to);
            }
            else
            {
                merged.Add(b);
            }
        }

        // Extract free slots
        var free = new List<(DateTime from, DateTime to)>();
        var cursor = dayStart;
        foreach (var b in merged)
        {
            if (cursor < b.from) free.Add((cursor, b.from));
            cursor = b.to > cursor ? b.to : cursor;
        }
        if (cursor < dayEnd) free.Add((cursor, dayEnd));

        return free;
    }

    private static List<(DateTime from, DateTime to, TimeSpan duration)> MergeSlots(
        List<(DateTime from, DateTime to)> slots)
    {
        if (slots.Count == 0) return new();

        var result = new List<(DateTime from, DateTime to, TimeSpan duration)>();
        foreach (var s in slots)
        {
            if (s.to - s.from >= TimeSpan.FromMinutes(30))
            {
                result.Add((s.from, s.to, s.to - s.from));
            }
        }
        return result;
    }
}
