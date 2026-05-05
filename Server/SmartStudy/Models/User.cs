using SmartStudy.DAL;

namespace SmartStudy.Models;

// Core user entity (PK is Email) plus auth, profile, integrations, and stress-score helpers.
public class User
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string? AuthProvider { get; set; }

    public bool OnboardingCompleted { get; set; } = false;

    // Google Calendar integration
    public string? GoogleCalendarAccessToken { get; set; }
    public string? GoogleCalendarRefreshToken { get; set; }
    public DateTime? LastCalendarSync { get; set; }

    // Composio integration
    public string? ComposioConnectedAccountId { get; set; }

    // Ruppinet integration
    public string? RuppinetId { get; set; }
    public string? RuppinetPassword { get; set; }
    public DateTime? LastRuppinetSync { get; set; }

    // Moodle integration
    public string? MoodleToken { get; set; }
    public DateTime? LastMoodleSync { get; set; }

    // Navigation properties
    public NotificationSettings? NotificationSettings { get; set; }
    public SchedulingPreferences? SchedulingPreferences { get; set; }
    public ICollection<UserCourse> UserCourses { get; set; } = new List<UserCourse>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<StudentTask> Tasks { get; set; } = new List<StudentTask>();
    public ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
    public ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
    public ICollection<Friendship> FriendshipsAsUser1 { get; set; } = new List<Friendship>();
    public ICollection<Friendship> FriendshipsAsUser2 { get; set; } = new List<Friendship>();
    public ICollection<SharedTaskMember> SharedTaskMemberships { get; set; } = new List<SharedTaskMember>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    // ───── Auth / profile (BLL methods folded in as static) ──────────────

    // Loads the full user row by email, or null if not found.
    public static User? GetByEmail(string email)
    {
        DBservices db = new DBservices();
        return db.GetUserByEmail(email);
    }

    // Returns true when an account exists for the email (used during registration).
    public static bool Exists(string email)
    {
        DBservices db = new DBservices();
        return db.UserExists(email);
    }

    // Inserts a new user row (with hashed password and optional auth provider).
    public static void Create(string email, string firstName, string lastName, string password, string? authProvider = null)
    {
        DBservices db = new DBservices();
        db.CreateUser(email, firstName, lastName, password, authProvider);
    }

    // Stores the password-reset token and its expiry on the user.
    public static void UpdateResetToken(string email, string token, DateTime expiry)
    {
        DBservices db = new DBservices();
        db.UpdateResetToken(email, token, expiry);
    }

    // Replaces the user's password hash and clears the reset token.
    public static void ResetPassword(string email, string newPassword)
    {
        DBservices db = new DBservices();
        db.ResetPassword(email, newPassword);
    }

    // Flips OnboardingCompleted to true for the user.
    public static void SetOnboardingComplete(string email)
    {
        DBservices db = new DBservices();
        db.SetOnboardingComplete(email);
    }

    // Updates first/last name (each parameter optional).
    public static void UpdateProfile(string email, string? firstName, string? lastName)
    {
        DBservices db = new DBservices();
        db.UpdateUserProfile(email, firstName, lastName);
    }

    // Stores or updates the user's encrypted Ruppinet credentials and last-sync time.
    public static void UpdateRuppinetFields(string email, string? ruppinetId, string? ruppinetPassword, DateTime? lastSync = null)
    {
        DBservices db = new DBservices();
        db.UpdateRuppinetFields(email, ruppinetId, ruppinetPassword, lastSync);
    }

    // Clears the user's Ruppinet credentials.
    public static void ClearRuppinet(string email)
    {
        DBservices db = new DBservices();
        db.ClearRuppinet(email);
    }

    // Clears the user's Moodle linkage data.
    public static void ClearMoodle(string email)
    {
        DBservices db = new DBservices();
        db.ClearMoodle(email);
    }

    // ───── Calendar sync (from CalendarSyncBLL) ───────────────────────────

    // Stores the Composio connected-account ID linking the user to Google Calendar.
    public static void UpdateComposioId(string email, string? composioId)
    {
        DBservices db = new DBservices();
        db.UpdateComposioId(email, composioId);
    }

    // Reverse-lookup — returns every user linked to the given Composio account.
    public static List<string> GetUsersByComposioId(string composioId)
    {
        DBservices db = new DBservices();
        return db.GetUsersByComposioId(composioId);
    }

    // Resets LastCalendarSync to NULL so the next sync re-imports everything.
    public static void ClearLastCalendarSync(string email)
    {
        DBservices db = new DBservices();
        db.ClearLastCalendarSync(email);
    }

    // Returns the number of personal events that came from Google Calendar.
    public static int CountGcalEvents(string email)
    {
        DBservices db = new DBservices();
        return db.CountGcalEvents(email);
    }

    // Returns the IDs of personal events that originated from Google Calendar.
    public static List<int> GetGcalPersonalEventIds(string email)
    {
        DBservices db = new DBservices();
        return db.GetGcalPersonalEventIds(email);
    }

    // Deletes any event row by ID (used to remove gcal-imported events on disconnect).
    public static void DeleteEventById(int eventId)
    {
        DBservices db = new DBservices();
        db.DeleteEventById(eventId);
    }

    // Clears all Google Calendar tokens and the Composio link from the user.
    public static void DisconnectGoogleCalendar(string email)
    {
        DBservices db = new DBservices();
        db.DisconnectGoogleCalendar(email);
    }

    // ───── Stress scoring (from StressService) ────────────────────────────

    // Computes the user's current stress score (0-100) from required vs available study hours.
    public static StressScoreDto GetStressScore(string email)
    {
        var db = new DBservices();
        var now = DateTime.Now;

        var prefs = db.GetSchedPrefsByEmail(email);
        double sleepHours = prefs?.SleepHoursPerDay ?? 8.0;
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        var allLeafTasks = db.GetIncompleteLeafTasks(email);
        var incompleteTasks = allLeafTasks.Where(t => t.DueDate != null).ToList();

        var upcomingExams = db.GetUpcomingExams(email, now.Date);
        var completedForML = db.GetCompletedTasksForML(email);

        var courseRatios = completedForML
            .GroupBy(t => t.CourseId)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => g.Average(t => t.ActualHours / t.EstimatedHours));

        double requiredHours = incompleteTasks
            .Where(t => t.EstimatedHours.HasValue)
            .Sum(t =>
            {
                var est = (double)t.EstimatedHours!.Value;
                if (courseRatios.TryGetValue(t.CourseId, out var ratio))
                    return est * ratio;
                return est;
            });

        requiredHours += upcomingExams
            .Where(e => (e.Date - now.Date).TotalDays <= 14)
            .Count() * 10.0;

        DateTime? nearestDeadline = null;
        var taskDeadlines = incompleteTasks.Where(t => t.DueDate > now).Select(t => t.DueDate!.Value);
        var examDeadlines = upcomingExams.Select(e => e.Date.Add(e.Time));
        var allDeadlines = taskDeadlines.Concat(examDeadlines).Where(d => d > now).ToList();
        if (allDeadlines.Count != 0)
            nearestDeadline = allDeadlines.Min();

        double availableHours;
        if (nearestDeadline.HasValue)
        {
            double totalHoursUntilDeadline = (nearestDeadline.Value - now).TotalHours;
            double daysUntilDeadline = totalHoursUntilDeadline / 24.0;
            double sleepTotal = daysUntilDeadline * sleepHours;

            var events = db.GetBaseEventsInRangeOrRecurring(email, now, nearestDeadline.Value);
            double eventHours = events
                .Where(e => e.From > now && e.From < nearestDeadline.Value && !e.Recurring)
                .Sum(e => (e.To - e.From).TotalHours);

            availableHours = Math.Max(1, totalHoursUntilDeadline - sleepTotal - eventHours);
        }
        else
        {
            availableHours = 168;
        }

        double score = requiredHours > 0
            ? Math.Min(100, (requiredHours / availableHours) * 100)
            : 0;

        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);

        var todayTaskEvents = db.GetTaskEventsInRange(email, todayStart, todayEnd);
        todayTaskEvents = todayTaskEvents
            .Where(te => te.Status == "Scheduled" || te.Status == "Partial")
            .ToList();

        var todayAllEvents = db.GetEventsInDateRange(email, todayStart, todayEnd);

        double todayStudyHours = todayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
        double todayOtherHours = todayAllEvents
            .Where(e => !todayTaskEvents.Any(te => te.EventId == e.EventId))
            .Sum(e => (e.To - e.From).TotalHours);
        double todayTotalHours = todayStudyHours + todayOtherHours;

        double studyLoad = maxDailyStudy > 0 ? (todayStudyHours / maxDailyStudy) * 100 : 0;
        double totalLoad = maxDailyTotal > 0 ? (todayTotalHours / maxDailyTotal) * 100 : 0;

        return new StressScoreDto
        {
            Score = Math.Round(score, 1),
            Level = GetStressLevel(score),
            Color = GetStressColor(score),
            RequiredHours = Math.Round(requiredHours, 1),
            AvailableHours = Math.Round(availableHours, 1),
            StudyLoad = Math.Round(Math.Min(100, studyLoad), 1),
            TotalLoad = Math.Round(Math.Min(100, totalLoad), 1),
            TotalScheduledHours = Math.Round(todayTotalHours, 1)
        };
    }

    // Computes a per-day stress score for each of the next 7 days for the analytics chart.
    public static List<WeeklyStressDto> GetWeeklyStress(string email)
    {
        var db = new DBservices();
        var now = DateTime.Now;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var result = new List<WeeklyStressDto>();

        var prefs = db.GetSchedPrefsByEmail(email);
        double maxDailyStudy = prefs?.MaxDailyStudyHours ?? 6.0;
        double maxDailyTotal = prefs?.MaxDailyTotalHours ?? 14.0;

        var weekEnd = startOfWeek.AddDays(7);
        var allLeafTasks = db.GetIncompleteLeafTasks(email);
        var allEvents = db.GetEventsInDateRange(email, startOfWeek, weekEnd);
        var allTaskEvents = db.GetTaskEventsInRange(email, startOfWeek, weekEnd);
        var upcomingExams = db.GetUpcomingExams(email, startOfWeek);

        for (int i = 0; i < 7; i++)
        {
            var day = startOfWeek.AddDays(i);
            var dayEnd = day.AddDays(1);

            var dayTasks = allLeafTasks
                .Count(t => t.DueDate != null && t.DueDate >= day && t.DueDate < dayEnd);

            var dayEvents = allEvents.Where(e => e.From >= day && e.From < dayEnd).ToList();

            var dayTaskEvents = allTaskEvents
                .Where(te => te.From >= day && te.From < dayEnd
                    && (te.Status == "Scheduled" || te.Status == "Partial"))
                .ToList();

            var dayExams = upcomingExams.Count(e => e.Date >= day && e.Date < dayEnd);

            double studyHours = dayTaskEvents.Sum(te => (te.To - te.From).TotalHours);
            double otherHours = dayEvents
                .Where(e => !dayTaskEvents.Any(te => te.EventId == e.EventId))
                .Sum(e => (e.To - e.From).TotalHours);
            double totalHours = studyHours + otherHours;

            double studyLoad = maxDailyStudy > 0 ? (studyHours / maxDailyStudy) * 100 : 0;
            double totalLoad = maxDailyTotal > 0 ? (totalHours / maxDailyTotal) * 100 : 0;
            double dayScore = Math.Min(100, Math.Max(studyLoad, totalLoad));

            result.Add(new WeeklyStressDto
            {
                DayName = day.ToString("ddd"),
                Date = day,
                Score = Math.Round(dayScore, 1),
                Level = GetStressLevel(dayScore),
                Color = GetStressColor(dayScore),
                TaskCount = dayTasks,
                EventCount = dayEvents.Count
            });
        }

        return result;
    }

    // Maps a numeric stress score to its label ("Low", "Moderate", "High").
    private static string GetStressLevel(double score) => score switch
    {
        <= 40 => "Low",
        <= 70 => "Moderate",
        _ => "High"
    };

    // Maps a numeric stress score to a hex color for the UI badge (green/orange/red).
    private static string GetStressColor(double score) => score switch
    {
        <= 40 => "#27AE60",
        <= 70 => "#F28D35",
        _ => "#E74C3C"
    };
}

// ───── Auth DTOs (from AuthDtos.cs) ─────────────────────────────────

// Request body for POST /api/auth/login.
public class LoginDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

// Request body for POST /api/auth/register.
public class RegisterDto
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Password { get; set; } = null!;
}

// Response payload for login/register/google endpoints — JWT plus user profile.
public class AuthResponseDto
{
    public string Token { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public bool OnboardingCompleted { get; set; }
    public bool IsNewUser { get; set; }
    public bool RuppinetSynced { get; set; }
}

// Public auth config returned by GET /api/auth/config (Google OAuth client ID).
public class AuthConfigDto
{
    public string? GoogleClientId { get; set; }
}

// Request body for POST /api/auth/forgot-password.
public class ForgotPasswordDto
{
    public string Email { get; set; } = null!;
}

// Request body for POST /api/auth/reset-password.
public class ResetPasswordDto
{
    public string Email { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}

// Request body for POST /api/auth/google — carries the Google ID token.
public class GoogleLoginDto
{
    public string IdToken { get; set; } = null!;
}

// ───── User profile DTOs (from DashboardDtos.cs) ────────────────────

// Wire-format payload for GET /api/settings/profile.
public class UserProfileDto
{
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public NotificationSettingsDto? NotificationSettings { get; set; }
}

// Request body for PUT /api/settings/profile.
public class UpdateProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

// ───── Stress score DTOs (from DashboardDtos.cs / SchedulingDtos.cs) ─

// Wire-format payload returned by GET /api/stress/score.
public class StressScoreDto
{
    public double Score { get; set; }
    public string Level { get; set; } = null!;
    public string Color { get; set; } = null!;
    public double RequiredHours { get; set; }
    public double AvailableHours { get; set; }
    public double StudyLoad { get; set; }
    public double TotalLoad { get; set; }
    public double TotalScheduledHours { get; set; }
}

// Single-day data point for the weekly stress chart.
public class WeeklyStressDto
{
    public string DayName { get; set; } = null!;
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public string Level { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int TaskCount { get; set; }
    public int EventCount { get; set; }
}

// ───── Moodle DTOs (from MoodleDtos.cs) ─────────────────────────────

// Result payload returned by Moodle sync endpoints (counts and warnings).
public class MoodleSyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
    public int TasksCreated { get; set; }
    public int TasksUpdated { get; set; }
    public int TasksSkipped { get; set; }
    public int CoursesMatched { get; set; }
    public int CoursesCreated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

// Wire-format response for GET /api/settings/moodle.
public class MoodleStatusDto
{
    public bool IsAvailable { get; set; }
    public DateTime? LastSync { get; set; }
}

// ───── Ruppinet DTOs (from RuppinetDtos.cs) ─────────────────────────

// Request body for POST /api/settings/ruppinet/connect — Ruppinet ID + password.
public class RuppinetConnectDto
{
    public string RuppinetId { get; set; } = null!;
    public string RuppinetPassword { get; set; } = null!;
}

// Result payload returned by Ruppinet sync endpoints (counts and warnings).
public class RuppinetSyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
    public int CoursesCreated { get; set; }
    public int CoursesUpdated { get; set; }
    public int ExamsCreated { get; set; }
    public int ExamsUpdated { get; set; }
    public int ClassEventsCreated { get; set; }
    public int ClassEventsSkipped { get; set; }
    public List<string> Warnings { get; set; } = new();
}

// Wire-format response for GET /api/settings/ruppinet.
public class RuppinetStatusDto
{
    public bool IsConnected { get; set; }
    public string? RuppinetId { get; set; }
    public DateTime? LastSync { get; set; }
}
