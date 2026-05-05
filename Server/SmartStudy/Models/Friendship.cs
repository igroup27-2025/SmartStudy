using SmartStudy.DAL;

namespace SmartStudy.Models;

// Active friendship between two users plus collaboration helpers (safe-zone calculation).
public class Friendship
{
    public int FriendshipId { get; set; }
    public string Email1 { get; set; } = null!; // Alphabetically smaller
    public string Email2 { get; set; } = null!; // Alphabetically larger
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User User1 { get; set; } = null!;
    public User User2 { get; set; } = null!;

    // ───── ConnectionsBLL methods folded in ───────────────────────────

    // Returns pending friend requests sent to or by the user.
    public static List<FriendRequestRow> GetFriendRequestsByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetFriendRequestsByUser(email);
    }

    // Returns the user's accepted friendships with friend names and emails.
    public static List<FriendshipRow> GetByUser(string email)
    {
        DBservices db = new DBservices();
        return db.GetFriendshipsByUser(email);
    }

    // Inserts a new pending friend request and returns its generated ID.
    public static int CreateFriendRequest(string requesterEmail, string addresseeEmail)
    {
        DBservices db = new DBservices();
        return db.CreateFriendRequest(requesterEmail, addresseeEmail);
    }

    // Updates a friend request's status if the addressee owns it; returns the basic row.
    public static FriendRequestBasic? UpdateFriendRequestStatus(int requestId, string addresseeEmail, string newStatus)
    {
        DBservices db = new DBservices();
        return db.UpdateFriendRequestStatus(requestId, addresseeEmail, newStatus);
    }

    // Materializes a Friendship row after both sides accept and returns its ID.
    public static int Create(string email1, string email2)
    {
        DBservices db = new DBservices();
        return db.CreateFriendship(email1, email2);
    }

    // Soft-deletes a friendship (IsActive=false) only if the user is one of the two parties.
    public static bool Deactivate(int friendshipId, string email)
    {
        DBservices db = new DBservices();
        return db.DeactivateFriendship(friendshipId, email);
    }

    // Returns true if there's an active friendship between the two emails.
    public static bool ExistsBetween(string email1, string email2)
    {
        DBservices db = new DBservices();
        return db.FriendshipExists(email1, email2);
    }

    // ───── From CollaborationBLL ──────────────────────────────────────

    // Returns a friendship by ID only if the user is one of the two parties.
    public static FriendshipBasic? GetForUser(int friendshipId, string email)
    {
        DBservices db = new DBservices();
        return db.GetFriendshipForUser(friendshipId, email);
    }

    // ───── From SafeZoneService ───────────────────────────────────────

    // Computes shared low-stress availability slots for the next 7 days between two friends.
    public static List<SafeZoneDto> GetSafeZones(string myEmail, string friendEmail)
    {
        var db = new DBservices();
        var now = DateTime.Now;
        var startDate = now.Date;
        var endDate = startDate.AddDays(7);

        var myStress = User.GetStressScore(myEmail);
        var friendStress = User.GetStressScore(friendEmail);

        var myEvents = StudentTask.GetExpandedEvents(db, myEmail, startDate, endDate);
        var friendEvents = StudentTask.GetExpandedEvents(db, friendEmail, startDate, endDate);

        var safeZones = new List<SafeZoneDto>();

        for (var day = startDate; day < endDate; day = day.AddDays(1))
        {
            var dayStart = day.AddHours(8);
            var dayEnd = day.AddHours(22);

            if (day == now.Date && now > dayStart)
            {
                var rounded = new DateTime(now.Year, now.Month, now.Day, now.Hour,
                    now.Minute >= 30 ? 30 : 0, 0).AddMinutes(30);
                if (rounded > dayStart) dayStart = rounded;
            }

            if (dayStart >= dayEnd) continue;

            var myBusy = GetBusyIntervals(myEvents, dayStart, dayEnd);
            var friendBusy = GetBusyIntervals(friendEvents, dayStart, dayEnd);

            var mutualFree = FindMutualFreeSlots(dayStart, dayEnd, myBusy, friendBusy);

            var merged = MergeSlots(mutualFree);

            foreach (var slot in merged)
            {
                if (slot.duration < TimeSpan.FromMinutes(30)) continue;

                if (myStress.Score >= 60 || friendStress.Score >= 60) continue;

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

    // Clamps events to the day window and merges overlapping busy intervals.
    private static List<(DateTime from, DateTime to)> GetBusyIntervals(
        List<Event> events, DateTime dayStart, DateTime dayEnd)
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

    // Computes the gaps left after subtracting the union of both users' busy intervals.
    private static List<(DateTime from, DateTime to)> FindMutualFreeSlots(
        DateTime dayStart, DateTime dayEnd,
        List<(DateTime from, DateTime to)> myBusy,
        List<(DateTime from, DateTime to)> friendBusy)
    {
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

    // Drops free slots shorter than 30 minutes and tags each remaining slot with its duration.
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

// ───── Connection / Safe zone DTOs (from ConnectionDtos.cs) ────────

// Wire-format payload for one connection (friend or pending request).
public class ConnectionDto
{
    public int ConnectionId { get; set; }
    public string FriendEmail { get; set; } = null!;
    public string FriendName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? ConnectedDate { get; set; }
}

// Request body for POST /api/connections/invite.
public class InviteConnectionDto
{
    public string Email { get; set; } = null!;
}

// Wire-format mutual-availability slot returned by GET /api/collaboration/safe-zones.
public class SafeZoneDto
{
    public string Date { get; set; } = null!;
    public string Day { get; set; } = null!;
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
    public double MyStress { get; set; }
    public double FriendStress { get; set; }
}
