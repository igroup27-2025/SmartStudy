using Microsoft.Data.SqlClient;
using System.Data;
using SmartStudy.Models;

namespace SmartStudy.DAL;

// Data-access layer wrapping every stored procedure call via ADO.NET.
public class DBservices
{
    // Empty constructor — connection is opened per call via connect().
    public DBservices()
    {
    }

    //--------------------------------------------------------------------------------------------------
    // This method creates a connection to the database according to the connectionString name in appsettings.json
    //--------------------------------------------------------------------------------------------------
    public SqlConnection connect(String conString)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json").Build();
        string cStr = configuration.GetConnectionString(conString);
        SqlConnection con = new SqlConnection(cStr);
        con.Open();
        return con;
    }

    //---------------------------------------------------------------------------------
    // Create the SqlCommand using a stored procedure
    //---------------------------------------------------------------------------------
    private SqlCommand CreateCommandWithStoredProcedureGeneral(String spName, SqlConnection con, Dictionary<string, object> paramDic)
    {
        SqlCommand cmd = new SqlCommand();
        cmd.Connection = con;
        cmd.CommandText = spName;
        cmd.CommandTimeout = 10;
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        if (paramDic != null)
            foreach (KeyValuePair<string, object> param in paramDic)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        return cmd;
    }

    // =====================================================
    // USERS
    // =====================================================

    // Loads the user row by email via SS_Users_GetByEmail.
    public User? GetUserByEmail(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_GetByEmail", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapUser(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true when an account with that email exists.
    public bool UserExists(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_ExistsByEmail", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a new user row with hashed password and auth provider.
    public void CreateUser(string email, string firstName, string lastName, string password, string? authProvider = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@FirstName", firstName);
        paramDic.Add("@LastName", lastName);
        paramDic.Add("@Password", password);
        paramDic.Add("@AuthProvider", (object?)authProvider ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_Create", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates the user's first/last name.
    public void UpdateUserProfile(string email, string? firstName, string? lastName)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@FirstName", (object?)firstName ?? DBNull.Value);
        paramDic.Add("@LastName", (object?)lastName ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateProfile", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Stores the password-reset token and its expiry on the user.
    public void UpdateResetToken(string email, string token, DateTime expiry)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@ResetToken", token);
        paramDic.Add("@ResetTokenExpiry", expiry);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateResetToken", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Replaces the user's password hash and clears the reset token.
    public void ResetPassword(string email, string newPassword)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@NewPassword", newPassword);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_ResetPassword", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Flips OnboardingCompleted to true for the user.
    public void SetOnboardingComplete(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_SetOnboardingComplete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Stores or updates the user's encrypted Ruppinet credentials.
    public void UpdateRuppinetFields(string email, string? ruppinetId, string? ruppinetPassword, DateTime? lastSync = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@RuppinetId", (object?)ruppinetId ?? DBNull.Value);
        paramDic.Add("@RuppinetPassword", (object?)ruppinetPassword ?? DBNull.Value);
        paramDic.Add("@LastRuppinetSync", (object?)lastSync ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateRuppinetFields", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Clears the user's stored Ruppinet credentials.
    public void ClearRuppinet(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_ClearRuppinet", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a User entity.
    private static User MapUser(SqlDataReader r)
    {
        return new User
        {
            Email = r.GetString(r.GetOrdinal("Email")),
            FirstName = r.GetString(r.GetOrdinal("FirstName")),
            LastName = r.GetString(r.GetOrdinal("LastName")),
            Password = r.GetString(r.GetOrdinal("Password")),
            ResetToken = r.IsDBNull(r.GetOrdinal("ResetToken")) ? null : r.GetString(r.GetOrdinal("ResetToken")),
            ResetTokenExpiry = r.IsDBNull(r.GetOrdinal("ResetTokenExpiry")) ? null : r.GetDateTime(r.GetOrdinal("ResetTokenExpiry")),
            AuthProvider = r.IsDBNull(r.GetOrdinal("AuthProvider")) ? null : r.GetString(r.GetOrdinal("AuthProvider")),
            OnboardingCompleted = Convert.ToBoolean(r.GetValue(r.GetOrdinal("OnboardingCompleted"))),
            GoogleCalendarAccessToken = r.IsDBNull(r.GetOrdinal("GoogleCalendarAccessToken")) ? null : r.GetString(r.GetOrdinal("GoogleCalendarAccessToken")),
            GoogleCalendarRefreshToken = r.IsDBNull(r.GetOrdinal("GoogleCalendarRefreshToken")) ? null : r.GetString(r.GetOrdinal("GoogleCalendarRefreshToken")),
            LastCalendarSync = r.IsDBNull(r.GetOrdinal("LastCalendarSync")) ? null : r.GetDateTime(r.GetOrdinal("LastCalendarSync")),
            ComposioConnectedAccountId = r.IsDBNull(r.GetOrdinal("ComposioConnectedAccountId")) ? null : r.GetString(r.GetOrdinal("ComposioConnectedAccountId")),
            RuppinetId = r.IsDBNull(r.GetOrdinal("RuppinetId")) ? null : r.GetString(r.GetOrdinal("RuppinetId")),
            RuppinetPassword = r.IsDBNull(r.GetOrdinal("RuppinetPassword")) ? null : r.GetString(r.GetOrdinal("RuppinetPassword")),
            LastRuppinetSync = r.IsDBNull(r.GetOrdinal("LastRuppinetSync")) ? null : r.GetDateTime(r.GetOrdinal("LastRuppinetSync")),
            MoodleToken = r.IsDBNull(r.GetOrdinal("MoodleToken")) ? null : r.GetString(r.GetOrdinal("MoodleToken")),
            LastMoodleSync = r.IsDBNull(r.GetOrdinal("LastMoodleSync")) ? null : r.GetDateTime(r.GetOrdinal("LastMoodleSync"))
        };
    }

    // =====================================================
    // NOTIFICATION SETTINGS
    // =====================================================

    // Loads the user's NotificationSettings row, or null.
    public NotificationSettings? GetNotifSettingsByEmail(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_NotifSettings_GetByEmail", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapNotifSettings(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts or updates the user's notification toggles and quiet-hours.
    public void UpsertNotifSettings(string email, bool notifyBeforeTask, bool dailyMorningSummary,
        bool weeklyPlanReminder, bool enablePushNotification, TimeSpan? quietStart, TimeSpan? quietEnd)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        cmd = new SqlCommand("SS_NotifSettings_Upsert", con);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 10;
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@NotifyBeforeTask", notifyBeforeTask);
        cmd.Parameters.AddWithValue("@DailyMorningSummary", dailyMorningSummary);
        cmd.Parameters.AddWithValue("@WeeklyPlanReminder", weeklyPlanReminder);
        cmd.Parameters.AddWithValue("@EnablePushNotification", enablePushNotification);
        cmd.Parameters.Add("@QuietHoursStart", SqlDbType.Time).Value = (object?)quietStart ?? DBNull.Value;
        cmd.Parameters.Add("@QuietHoursEnd", SqlDbType.Time).Value = (object?)quietEnd ?? DBNull.Value;

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a default-toggles NotificationSettings row for a new user.
    public void CreateDefaultNotifSettings(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_NotifSettings_CreateDefault", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a NotificationSettings entity.
    private static NotificationSettings MapNotifSettings(SqlDataReader r)
    {
        return new NotificationSettings
        {
            Email = r.GetString(r.GetOrdinal("Email")),
            NotifyBeforeTask = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Notify_before_task"))),
            DailyMorningSummary = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Daily_morning_summary"))),
            WeeklyPlanReminder = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Weekly_plan_reminder"))),
            EnablePushNotification = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Enable_push_notification"))),
            QuietHoursStart = r.IsDBNull(r.GetOrdinal("Quiet_hours_start")) ? null : r.GetTimeSpan(r.GetOrdinal("Quiet_hours_start")),
            QuietHoursEnd = r.IsDBNull(r.GetOrdinal("Quiet_hours_end")) ? null : r.GetTimeSpan(r.GetOrdinal("Quiet_hours_end"))
        };
    }

    // =====================================================
    // SCHEDULING PREFERENCES
    // =====================================================

    // Loads the user's SchedulingPreferences row, or null.
    public SchedulingPreferences? GetSchedPrefsByEmail(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SchedPrefs_GetByEmail", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapSchedPrefs(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts or updates the user's auto-scheduler preferences.
    public void UpsertSchedPrefs(SchedulingPreferences p)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        cmd = new SqlCommand("SS_SchedPrefs_Upsert", con);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 10;
        cmd.Parameters.AddWithValue("@Email", p.Email);
        cmd.Parameters.AddWithValue("@MaxDailyStudyHours", p.MaxDailyStudyHours);
        cmd.Parameters.AddWithValue("@MaxContinuousMinutes", p.MaxContinuousMinutes);
        cmd.Parameters.AddWithValue("@DayStartHour", p.DayStartHour);
        cmd.Parameters.AddWithValue("@DayEndHour", p.DayEndHour);
        cmd.Parameters.AddWithValue("@SleepHoursPerDay", p.SleepHoursPerDay);
        cmd.Parameters.Add("@LunchBreakStart", SqlDbType.Time).Value = (object?)p.LunchBreakStart ?? DBNull.Value;
        cmd.Parameters.Add("@LunchBreakEnd", SqlDbType.Time).Value = (object?)p.LunchBreakEnd ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@BreakDurationMinutes", p.BreakDurationMinutes);
        cmd.Parameters.AddWithValue("@DefaultTaskEstimatedHours", p.DefaultTaskEstimatedHours);
        cmd.Parameters.AddWithValue("@MaxDailyTotalHours", p.MaxDailyTotalHours);
        cmd.Parameters.AddWithValue("@ExamPrepHoursPerDay", p.ExamPrepHoursPerDay);
        cmd.Parameters.AddWithValue("@ExamPrepDays", p.ExamPrepDays);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a SchedulingPreferences entity.
    private static SchedulingPreferences MapSchedPrefs(SqlDataReader r)
    {
        return new SchedulingPreferences
        {
            Email = r.GetString(r.GetOrdinal("Email")),
            MaxDailyStudyHours = r.GetDouble(r.GetOrdinal("MaxDailyStudyHours")),
            MaxContinuousMinutes = r.GetInt32(r.GetOrdinal("MaxContinuousMinutes")),
            DayStartHour = r.GetInt32(r.GetOrdinal("DayStartHour")),
            DayEndHour = r.GetInt32(r.GetOrdinal("DayEndHour")),
            SleepHoursPerDay = r.GetDouble(r.GetOrdinal("SleepHoursPerDay")),
            LunchBreakStart = r.IsDBNull(r.GetOrdinal("LunchBreakStart")) ? null : r.GetTimeSpan(r.GetOrdinal("LunchBreakStart")),
            LunchBreakEnd = r.IsDBNull(r.GetOrdinal("LunchBreakEnd")) ? null : r.GetTimeSpan(r.GetOrdinal("LunchBreakEnd")),
            BreakDurationMinutes = r.GetInt32(r.GetOrdinal("BreakDurationMinutes")),
            DefaultTaskEstimatedHours = r.GetDouble(r.GetOrdinal("DefaultTaskEstimatedHours")),
            MaxDailyTotalHours = r.GetDouble(r.GetOrdinal("MaxDailyTotalHours")),
            ExamPrepHoursPerDay = r.GetDouble(r.GetOrdinal("ExamPrepHoursPerDay")),
            ExamPrepDays = r.GetInt32(r.GetOrdinal("ExamPrepDays"))
        };
    }

    // =====================================================
    // INSTRUCTORS
    // =====================================================

    // Returns the global list of instructors.
    public List<Instructor> GetAllInstructors()
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Instructors_GetAll", con, null);

        try
        {
            List<Instructor> list = new List<Instructor>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new Instructor
                {
                    InstructorId = reader.GetInt32(reader.GetOrdinal("InstructorId")),
                    InstructorName = reader.GetString(reader.GetOrdinal("InstructorName"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // =====================================================
    // EVENTS (Full CRUD - Phase 5)
    // =====================================================

    // Returns every event in a date window with subtype data flattened.
    public List<TypedEvent> GetAllTypedEventsInRange(string email, DateTime? from, DateTime? to)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", (object?)from ?? DBNull.Value);
        paramDic.Add("@To", (object?)to ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_GetAllTypedInRange", con, paramDic);

        try
        {
            List<TypedEvent> list = new List<TypedEvent>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTypedEvent(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a class event tied to a course and returns its ID.
    public int CreateClassEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@Location", (object?)location ?? DBNull.Value);
        paramDic.Add("@Duration", (object?)duration ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_ClassEvents_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a task study-block event linking back to a task; returns its ID.
    public int CreateTaskEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Priority", (object?)priority ?? DBNull.Value);
        paramDic.Add("@Status", status ?? "Scheduled");

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a work-shift event and returns its ID.
    public int CreateWorkEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@WorkPlace", (object?)workPlace ?? DBNull.Value);
        paramDic.Add("@TravelTime", (object?)travelTime ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_WorkEvents_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a personal event (sleep/meal/etc.) and returns its ID.
    public int CreatePersonalEvent(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@Type", (object?)type ?? DBNull.Value);
        paramDic.Add("@Description", (object?)description ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_PersonalEvents_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a class event's fields.
    public void UpdateClassEvent(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@Location", (object?)location ?? DBNull.Value);
        paramDic.Add("@Duration", (object?)duration ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_ClassEvents_Update", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a task-event's fields (used when the user drags a study block).
    public void UpdateTaskEvent(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Priority", (object?)priority ?? DBNull.Value);
        paramDic.Add("@Status", (object?)status ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_Update", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the partner's copy-task ID for a shared task, or null.
    public int? GetSharedPartnerTaskId(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_GetPartnerTaskId", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return null;
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Mirrors a moved task-event onto the partner's calendar.
    public int? SyncSharedTaskEventMove(int movedEventId, int partnerTaskId,
        DateTime oldFrom, DateTime oldTo, DateTime newFrom, DateTime newTo)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@MovedEventId", movedEventId);
        paramDic.Add("@PartnerTaskId", partnerTaskId);
        paramDic.Add("@OldFrom", oldFrom);
        paramDic.Add("@OldTo", oldTo);
        paramDic.Add("@NewFrom", newFrom);
        paramDic.Add("@NewTo", newTo);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_SyncSharedMove", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return null;
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    public (DateTime From, DateTime To)? GetEventTimeRange(int eventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_GetTimeRange", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return (reader.GetDateTime(reader.GetOrdinal("From")), reader.GetDateTime(reader.GetOrdinal("To")));
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a work-event's fields.
    public void UpdateWorkEvent(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@WorkPlace", (object?)workPlace ?? DBNull.Value);
        paramDic.Add("@TravelTime", (object?)travelTime ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_WorkEvents_Update", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a personal-event's fields.
    public void UpdatePersonalEvent(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Recurring", recurring);
        paramDic.Add("@RecurrenceEndDate", (object?)recurrenceEndDate ?? DBNull.Value);
        paramDic.Add("@Type", (object?)type ?? DBNull.Value);
        paramDic.Add("@Description", (object?)description ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_PersonalEvents_Update", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Deletes an event row (cascades to subtype rows).
    public void DeleteEvent(int eventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_Delete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the email of the event's owner, or null.
    public string? GetEventOwnerEmail(int eventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_GetOwnerEmail", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the event's subtype string (class/task/work/personal).
    public string GetEventSubtype(int eventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_GetSubtype", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return result?.ToString() ?? "unknown";
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Swaps an event's subtype rows (Work ↔ Personal).
    public void ChangeEventType(int eventId, string oldType, string newType, string? workPlace, int? travelTime, string? personalType, string? description)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);
        paramDic.Add("@OldType", oldType);
        paramDic.Add("@NewType", newType);
        paramDic.Add("@WorkPlace", (object?)workPlace ?? DBNull.Value);
        paramDic.Add("@TravelTime", (object?)travelTime ?? DBNull.Value);
        paramDic.Add("@Type", (object?)personalType ?? DBNull.Value);
        paramDic.Add("@Description", (object?)description ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_ChangeType", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Counts task-event blocks overlapping the time window (excluding one event).
    public int CountConflictingTaskEvents(string email, DateTime from, DateTime to, int excludeEventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@ExcludeEventId", excludeEventId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_CountConflictingTaskEvents", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns events overlapping the given time window.
    public List<TypedEvent> GetConflictingEvents(string email, DateTime from, DateTime to, int? excludeEventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@ExcludeEventId", (object?)excludeEventId ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_CheckConflicts", con, paramDic);

        try
        {
            List<TypedEvent> list = new List<TypedEvent>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTypedEvent(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Marks a task as manually pinned so the auto-scheduler won't move it.
    public void PinTask(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_PinTask", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true if the SqlDataReader has a column with the given name.
    private static bool HasColumn(SqlDataReader r, string name)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // Reads a SqlDataReader row into a TypedEvent (subtype-aware) record.
    private static TypedEvent MapTypedEvent(SqlDataReader r)
    {
        return new TypedEvent
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Recurring = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Recurring"))),
            RecurrenceEndDate = r.IsDBNull(r.GetOrdinal("RecurrenceEndDate")) ? null : r.GetDateTime(r.GetOrdinal("RecurrenceEndDate")),
            EventType = r.GetString(r.GetOrdinal("EventType")),
            // ClassEvent
            CourseId = r.IsDBNull(r.GetOrdinal("CourseId")) ? null : r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.IsDBNull(r.GetOrdinal("CourseName")) ? null : r.GetString(r.GetOrdinal("CourseName")),
            Location = r.IsDBNull(r.GetOrdinal("Location")) ? null : r.GetString(r.GetOrdinal("Location")),
            Duration = r.IsDBNull(r.GetOrdinal("Duration")) ? null : r.GetDecimal(r.GetOrdinal("Duration")),
            // TaskEvent
            TaskId = r.IsDBNull(r.GetOrdinal("TaskId")) ? null : r.GetInt32(r.GetOrdinal("TaskId")),
            TaskTitle = r.IsDBNull(r.GetOrdinal("TaskTitle")) ? null : r.GetString(r.GetOrdinal("TaskTitle")),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            ActualHours = r.IsDBNull(r.GetOrdinal("ActualHours")) ? null : r.GetDecimal(r.GetOrdinal("ActualHours")),
            Status = r.IsDBNull(r.GetOrdinal("Status")) ? null : r.GetString(r.GetOrdinal("Status")),
            IsManuallyPinned = r.IsDBNull(r.GetOrdinal("IsManuallyPinned")) ? null : Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsManuallyPinned"))),
            IsShared = HasColumn(r, "IsShared") && !r.IsDBNull(r.GetOrdinal("IsShared"))
                && Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsShared"))),
            SharedStatus = HasColumn(r, "SharedStatus") && !r.IsDBNull(r.GetOrdinal("SharedStatus"))
                ? r.GetString(r.GetOrdinal("SharedStatus")) : null,
            // WorkEvent
            TravelTime = r.IsDBNull(r.GetOrdinal("TravelTime")) ? null : r.GetInt32(r.GetOrdinal("TravelTime")),
            WorkPlace = r.IsDBNull(r.GetOrdinal("WorkPlace")) ? null : r.GetString(r.GetOrdinal("WorkPlace")),
            // PersonalEvent
            Type = r.IsDBNull(r.GetOrdinal("Type")) ? null : r.GetString(r.GetOrdinal("Type")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description"))
        };
    }

    // =====================================================
    // COURSES
    // =====================================================

    // Returns the user's enrolled courses with task/exam counts.
    public List<CourseWithEnrollment> GetCoursesByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_GetByUser", con, paramDic);

        try
        {
            List<CourseWithEnrollment> list = new List<CourseWithEnrollment>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new CourseWithEnrollment
                {
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                    WeeklyHours = reader.IsDBNull(reader.GetOrdinal("WeeklyHours")) ? null : reader.GetDecimal(reader.GetOrdinal("WeeklyHours")),
                    Credits = reader.IsDBNull(reader.GetOrdinal("Credits")) ? null : reader.GetDecimal(reader.GetOrdinal("Credits")),
                    Semester = reader.IsDBNull(reader.GetOrdinal("Semester")) ? null : reader.GetString(reader.GetOrdinal("Semester")),
                    InstructorId = reader.IsDBNull(reader.GetOrdinal("InstructorId")) ? null : reader.GetInt32(reader.GetOrdinal("InstructorId")),
                    InstructorName = reader.IsDBNull(reader.GetOrdinal("InstructorName")) ? null : reader.GetString(reader.GetOrdinal("InstructorName")),
                    DefaultTaskEstimatedHours = reader.IsDBNull(reader.GetOrdinal("DefaultTaskEstimatedHours")) ? null : reader.GetDouble(reader.GetOrdinal("DefaultTaskEstimatedHours")),
                    ExamPrepHoursPerDay = reader.IsDBNull(reader.GetOrdinal("ExamPrepHoursPerDay")) ? null : reader.GetDouble(reader.GetOrdinal("ExamPrepHoursPerDay")),
                    ExamPrepDays = reader.IsDBNull(reader.GetOrdinal("ExamPrepDays")) ? null : reader.GetInt32(reader.GetOrdinal("ExamPrepDays")),
                    StudyPartnerEmail = reader.IsDBNull(reader.GetOrdinal("StudyPartnerEmail")) ? null : reader.GetString(reader.GetOrdinal("StudyPartnerEmail")),
                    SharedByDefault = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("SharedByDefault"))),
                    CourseShareApproved = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("CourseShareApproved"))),
                    TaskCount = reader.GetInt32(reader.GetOrdinal("TaskCount")),
                    ExamCount = reader.GetInt32(reader.GetOrdinal("ExamCount"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Loads a course by ID from the global table, or null.
    public Course? GetCourseById(int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_GetById", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapCourse(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the highest existing course ID for allocating the next manual course.
    public int GetMaxCourseId()
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_GetMaxId", con, null);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result ?? 0);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a new course row with optional credits/hours/semester/instructor.
    public void CreateCourse(int courseId, string courseName, decimal? weeklyHours, decimal? credits, string? semester, int? instructorId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@CourseName", courseName);
        paramDic.Add("@WeeklyHours", (object?)weeklyHours ?? DBNull.Value);
        paramDic.Add("@Credits", (object?)credits ?? DBNull.Value);
        paramDic.Add("@Semester", (object?)semester ?? DBNull.Value);
        paramDic.Add("@InstructorId", (object?)instructorId ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_Create", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates any subset of a course's fields.
    public void UpdateCourse(int courseId, string? courseName = null, decimal? weeklyHours = null, decimal? credits = null,
        string? semester = null, int? instructorId = null, double? defaultTaskEstimatedHours = null, double? examPrepHoursPerDay = null, int? examPrepDays = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@CourseName", (object?)courseName ?? DBNull.Value);
        paramDic.Add("@WeeklyHours", (object?)weeklyHours ?? DBNull.Value);
        paramDic.Add("@Credits", (object?)credits ?? DBNull.Value);
        paramDic.Add("@Semester", (object?)semester ?? DBNull.Value);
        paramDic.Add("@InstructorId", (object?)instructorId ?? DBNull.Value);
        paramDic.Add("@DefaultTaskEstimatedHours", (object?)defaultTaskEstimatedHours ?? DBNull.Value);
        paramDic.Add("@ExamPrepHoursPerDay", (object?)examPrepHoursPerDay ?? DBNull.Value);
        paramDic.Add("@ExamPrepDays", (object?)examPrepDays ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_Update", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a Course entity.
    private static Course MapCourse(SqlDataReader r)
    {
        return new Course
        {
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            WeeklyHours = r.IsDBNull(r.GetOrdinal("WeeklyHours")) ? null : r.GetDecimal(r.GetOrdinal("WeeklyHours")),
            Credits = r.IsDBNull(r.GetOrdinal("Credits")) ? null : r.GetDecimal(r.GetOrdinal("Credits")),
            Semester = r.IsDBNull(r.GetOrdinal("Semester")) ? null : r.GetString(r.GetOrdinal("Semester")),
            InstructorId = r.IsDBNull(r.GetOrdinal("InstructorId")) ? null : r.GetInt32(r.GetOrdinal("InstructorId")),
            DefaultTaskEstimatedHours = r.IsDBNull(r.GetOrdinal("DefaultTaskEstimatedHours")) ? null : r.GetDouble(r.GetOrdinal("DefaultTaskEstimatedHours")),
            ExamPrepHoursPerDay = r.IsDBNull(r.GetOrdinal("ExamPrepHoursPerDay")) ? null : r.GetDouble(r.GetOrdinal("ExamPrepHoursPerDay")),
            ExamPrepDays = r.IsDBNull(r.GetOrdinal("ExamPrepDays")) ? null : r.GetInt32(r.GetOrdinal("ExamPrepDays"))
        };
    }

    // =====================================================
    // USER COURSES
    // =====================================================

    // Returns true when the user is enrolled in the course.
    public bool UserCourseExists(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_Exists", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a row into the UserCourses junction enrolling the user.
    public void CreateUserCourse(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_Create", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Removes the user's enrollment in a course.
    public void DeleteUserCourse(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_Delete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Sets or clears the study partner on the user's enrollment.
    public void UpdateStudyPartner(string email, int courseId, string? partnerEmail)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@StudyPartnerEmail", (object?)partnerEmail ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_UpdatePartner", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Sets whether the user's tasks for this course are auto-shared.
    public void UpdateSharedByDefault(string email, int courseId, bool sharedByDefault)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@SharedByDefault", sharedByDefault);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_UpdateSharedByDefault", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the IDs of every course the user is enrolled in.
    public List<int> GetCourseIdsByEmail(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_GetCourseIdsByEmail", con, paramDic);

        try
        {
            List<int> list = new List<int>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetInt32(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // =====================================================
    // EXAMS
    // =====================================================

    // Returns all exams for the user's enrolled courses.
    public List<ExamWithCourse> GetExamsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_GetByUser", con, paramDic);

        try
        {
            List<ExamWithCourse> list = new List<ExamWithCourse>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapExamWithCourse(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Loads one exam by ID if it belongs to a course the user is enrolled in.
    public ExamWithCourse? GetExamById(int examId, string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ExamId", examId);
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_GetById", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapExamWithCourse(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a new exam row and returns its ID.
    public int CreateExam(int courseId, DateTime date, TimeSpan time, string session, int? duration, bool isTakingExam)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        cmd = new SqlCommand("SS_Exams_Create", con);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 10;
        cmd.Parameters.AddWithValue("@CourseId", courseId);
        cmd.Parameters.AddWithValue("@Date", date);
        cmd.Parameters.Add("@Time", SqlDbType.Time).Value = time;
        cmd.Parameters.AddWithValue("@Session", session);
        cmd.Parameters.AddWithValue("@Duration", (object?)duration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsTakingExam", isTakingExam);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates any subset of an exam's fields.
    public void UpdateExam(int examId, int? courseId = null, DateTime? date = null, TimeSpan? time = null, string? session = null, int? duration = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        cmd = new SqlCommand("SS_Exams_Update", con);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 10;
        cmd.Parameters.AddWithValue("@ExamId", examId);
        cmd.Parameters.AddWithValue("@CourseId", (object?)courseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Date", (object?)date ?? DBNull.Value);
        cmd.Parameters.Add("@Time", SqlDbType.Time).Value = (object?)time ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@Session", (object?)session ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Duration", (object?)duration ?? DBNull.Value);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Flips the IsTakingExam flag on an exam.
    public void ToggleExamTaking(int examId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ExamId", examId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_ToggleTaking", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Deletes an exam row.
    public void DeleteExam(int examId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ExamId", examId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_Delete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Removes auto-generated exam-prep tasks when the user opts out.
    public void DeleteStudyTasksForExam(string email, int courseId, DateTime examDate)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@ExamDate", examDate);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_DeleteStudyTasksForExam", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into an ExamWithCourse record.
    private static ExamWithCourse MapExamWithCourse(SqlDataReader r)
    {
        return new ExamWithCourse
        {
            ExamId = r.GetInt32(r.GetOrdinal("ExamId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            Date = r.GetDateTime(r.GetOrdinal("Date")),
            Time = r.GetTimeSpan(r.GetOrdinal("Time")),
            Session = r.GetString(r.GetOrdinal("Session")),
            Duration = r.IsDBNull(r.GetOrdinal("Duration")) ? null : r.GetInt32(r.GetOrdinal("Duration")),
            IsTakingExam = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsTakingExam"))),
            ExamPrepHoursPerDay = r.IsDBNull(r.GetOrdinal("ExamPrepHoursPerDay")) ? null : r.GetDouble(r.GetOrdinal("ExamPrepHoursPerDay")),
            ExamPrepDays = r.IsDBNull(r.GetOrdinal("ExamPrepDays")) ? null : r.GetInt32(r.GetOrdinal("ExamPrepDays"))
        };
    }

    // =====================================================
    // TASKS
    // =====================================================

    // Returns the user's tasks (joined with course name) with optional filters.
    public List<TaskWithCourse> GetTasksByUser(string email, int? courseId = null, bool? completed = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", (object?)courseId ?? DBNull.Value);
        paramDic.Add("@Completed", (object?)completed ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetByUser", con, paramDic);

        try
        {
            List<TaskWithCourse> list = new List<TaskWithCourse>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTaskWithCourse(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns immediate child subtasks of a parent task.
    public List<TaskWithCourse> GetSubTasks(int parentTaskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ParentTaskId", parentTaskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetSubTasks", con, paramDic);

        try
        {
            List<TaskWithCourse> list = new List<TaskWithCourse>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTaskWithCourse(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Loads a single task by ID joined with its course, or null.
    public TaskWithCourse? GetTaskById(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetById", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapTaskWithCourse(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a new task and returns its generated ID.
    public int CreateTask(int courseId, string email, string title, string type,
        decimal? estimatedHours, DateTime? dueDate, int? parentTaskId, bool allowSplitting,
        string? priority, bool isManualPriority, bool isManuallyPinned = false)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@Email", email);
        paramDic.Add("@Title", title);
        paramDic.Add("@Type", type);
        paramDic.Add("@EstimatedHours", (object?)estimatedHours ?? DBNull.Value);
        paramDic.Add("@DueDate", (object?)dueDate ?? DBNull.Value);
        paramDic.Add("@ParentTaskId", (object?)parentTaskId ?? DBNull.Value);
        paramDic.Add("@AllowSplitting", allowSplitting);
        paramDic.Add("@Priority", (object?)priority ?? DBNull.Value);
        paramDic.Add("@IsManualPriority", isManualPriority);
        paramDic.Add("@IsManuallyPinned", isManuallyPinned);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates any subset of a task's fields.
    public void UpdateTask(int taskId, int? courseId = null, string? title = null, string? type = null,
        decimal? estimatedHours = null, DateTime? dueDate = null, bool? isCompleted = null,
        bool? allowSplitting = null, bool? isManuallyPinned = null, string? priority = null,
        bool? isManualPriority = null, decimal? actualHours = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@CourseId", (object?)courseId ?? DBNull.Value);
        paramDic.Add("@Title", (object?)title ?? DBNull.Value);
        paramDic.Add("@Type", (object?)type ?? DBNull.Value);
        paramDic.Add("@EstimatedHours", (object?)estimatedHours ?? DBNull.Value);
        paramDic.Add("@DueDate", (object?)dueDate ?? DBNull.Value);
        paramDic.Add("@IsCompleted", (object?)isCompleted ?? DBNull.Value);
        paramDic.Add("@AllowSplitting", (object?)allowSplitting ?? DBNull.Value);
        paramDic.Add("@IsManuallyPinned", (object?)isManuallyPinned ?? DBNull.Value);
        paramDic.Add("@Priority", (object?)priority ?? DBNull.Value);
        paramDic.Add("@IsManualPriority", (object?)isManualPriority ?? DBNull.Value);
        paramDic.Add("@ActualHours", (object?)actualHours ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_Update", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Deletes a task and its subtasks/task-events.
    public void DeleteTask(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_Delete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Toggles task completion and optionally records actualHours.
    public void CompleteTask(int taskId, bool isCompleted, decimal? actualHours = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@IsCompleted", isCompleted);
        paramDic.Add("@ActualHours", (object?)actualHours ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_Complete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the scheduled study-block events for a task.
    public List<TaskEventInfo> GetTaskEvents(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetTaskEvents", con, paramDic);

        try
        {
            List<TaskEventInfo> list = new List<TaskEventInfo>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new TaskEventInfo
                {
                    EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                    From = reader.GetDateTime(reader.GetOrdinal("From")),
                    To = reader.GetDateTime(reader.GetOrdinal("To")),
                    Priority = reader.IsDBNull(reader.GetOrdinal("Priority")) ? null : reader.GetString(reader.GetOrdinal("Priority")),
                    ActualHours = reader.IsDBNull(reader.GetOrdinal("ActualHours")) ? null : reader.GetDecimal(reader.GetOrdinal("ActualHours")),
                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? null : reader.GetString(reader.GetOrdinal("Status"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns sharing info if the task is shared, else null.
    public SharedTaskInfo? GetSharedInfo(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetSharedInfo", con, paramDic);

        try
        {
            var rows = new List<(int TaskId, string SharedStatus, string CreatedByEmail, string? MemberEmail, string? ResponseStatus, string? FirstName, string? LastName)>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt32(reader.GetOrdinal("TaskId")),
                    reader.GetString(reader.GetOrdinal("SharedStatus")),
                    reader.GetString(reader.GetOrdinal("CreatedByEmail")),
                    reader.IsDBNull(reader.GetOrdinal("MemberEmail")) ? null : reader.GetString(reader.GetOrdinal("MemberEmail")),
                    reader.IsDBNull(reader.GetOrdinal("ResponseStatus")) ? null : reader.GetString(reader.GetOrdinal("ResponseStatus")),
                    reader.IsDBNull(reader.GetOrdinal("FirstName")) ? null : reader.GetString(reader.GetOrdinal("FirstName")),
                    reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName"))
                ));
            }

            if (!rows.Any()) return null;
            var first = rows.First();
            return new SharedTaskInfo
            {
                TaskId = first.TaskId,
                SharedStatus = first.SharedStatus,
                CreatedByEmail = first.CreatedByEmail,
                Members = rows.Where(r => r.MemberEmail != null).Select(r => new SharedTaskMemberInfo
                {
                    Email = r.MemberEmail!,
                    ResponseStatus = r.ResponseStatus ?? "Pending",
                    FullName = $"{r.FirstName} {r.LastName}"
                }).ToList()
            };
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true if every subtask of the parent is completed.
    public bool CheckAllSiblingsComplete(int parentTaskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ParentTaskId", parentTaskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_CheckAllSiblingsComplete", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns completed-task estimated/actual pairs for one course.
    public List<MLDataRow> GetMLData(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetMLData", con, paramDic);

        try
        {
            List<MLDataRow> list = new List<MLDataRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new MLDataRow
                {
                    ActualHours = reader.GetDecimal(reader.GetOrdinal("ActualHours")),
                    EstimatedHours = reader.GetDecimal(reader.GetOrdinal("EstimatedHours"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns per-course estimation accuracy stats from completed tasks.
    public List<MLInsightRow> GetMLInsights(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetMLInsights", con, paramDic);

        try
        {
            List<MLInsightRow> list = new List<MLInsightRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new MLInsightRow
                {
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                    TaskCount = reader.GetInt32(reader.GetOrdinal("TaskCount")),
                    AvgEstimated = reader.GetDouble(reader.GetOrdinal("AvgEstimated")),
                    AvgActual = reader.GetDouble(reader.GetOrdinal("AvgActual")),
                    Accuracy = reader.GetDouble(reader.GetOrdinal("Accuracy"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts the SharedTask row that turns a task into a shared one.
    public void CreateSharedTask(int taskId, string createdByEmail, string sharedStatus = "Pending")
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@CreatedByEmail", createdByEmail);
        paramDic.Add("@SharedStatus", sharedStatus);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_Create", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a shared task's status (Pending/Confirmed/Cancelled).
    public void UpdateSharedTaskStatus(int taskId, string status)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@SharedStatus", status);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_UpdateStatus", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a member into a shared task with their initial response.
    public void CreateSharedTaskMember(int taskId, string email, string responseStatus = "Pending", DateTime? respondedAt = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Email", email);
        paramDic.Add("@ResponseStatus", responseStatus);
        paramDic.Add("@RespondedAt", (object?)respondedAt ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTaskMembers_Create", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the user's enrollment row for a course, or null.
    public UserCourse? GetUserCourse(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_GetByEmailAndCourse", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new UserCourse
                {
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    StudyPartnerEmail = reader.IsDBNull(reader.GetOrdinal("StudyPartnerEmail")) ? null : reader.GetString(reader.GetOrdinal("StudyPartnerEmail")),
                    SharedByDefault = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("SharedByDefault"))),
                    CourseShareApproved = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("CourseShareApproved")))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a TaskWithCourse record.
    private static TaskWithCourse MapTaskWithCourse(SqlDataReader r)
    {
        return new TaskWithCourse
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            Title = r.GetString(r.GetOrdinal("Title")),
            Type = r.GetString(r.GetOrdinal("Type")),
            EstimatedHours = r.IsDBNull(r.GetOrdinal("EstimatedHours")) ? null : r.GetDecimal(r.GetOrdinal("EstimatedHours")),
            DueDate = r.IsDBNull(r.GetOrdinal("DueDate")) ? null : r.GetDateTime(r.GetOrdinal("DueDate")),
            IsCompleted = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsCompleted"))),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            ActualHours = r.IsDBNull(r.GetOrdinal("ActualHours")) ? null : r.GetDecimal(r.GetOrdinal("ActualHours")),
            ParentTaskId = r.IsDBNull(r.GetOrdinal("ParentTaskId")) ? null : r.GetInt32(r.GetOrdinal("ParentTaskId")),
            AllowSplitting = Convert.ToBoolean(r.GetValue(r.GetOrdinal("AllowSplitting"))),
            IsManuallyPinned = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsManuallyPinned"))),
            IsManualPriority = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsManualPriority"))),
            Email = r.GetString(r.GetOrdinal("Email"))
        };
    }

    // =====================================================
    // FRIEND REQUESTS
    // =====================================================

    // Returns pending friend requests sent to or by the user.
    public List<FriendRequestRow> GetFriendRequestsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_FriendRequests_GetByUser", con, paramDic);

        try
        {
            List<FriendRequestRow> list = new List<FriendRequestRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new FriendRequestRow
                {
                    RequestId = reader.GetInt32(reader.GetOrdinal("RequestId")),
                    RequesterEmail = reader.GetString(reader.GetOrdinal("RequesterEmail")),
                    AddresseeEmail = reader.GetString(reader.GetOrdinal("AddresseeEmail")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    RequestedAt = reader.GetDateTime(reader.GetOrdinal("RequestedAt")),
                    RespondedAt = reader.IsDBNull(reader.GetOrdinal("RespondedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("RespondedAt")),
                    FriendEmail = reader.GetString(reader.GetOrdinal("FriendEmail")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a new pending friend request and returns its ID.
    public int CreateFriendRequest(string requesterEmail, string addresseeEmail)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@RequesterEmail", requesterEmail);
        paramDic.Add("@AddresseeEmail", addresseeEmail);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_FriendRequests_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a friend request's status if the addressee owns it.
    public FriendRequestBasic? UpdateFriendRequestStatus(int requestId, string addresseeEmail, string newStatus)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@RequestId", requestId);
        paramDic.Add("@AddresseeEmail", addresseeEmail);
        paramDic.Add("@NewStatus", newStatus);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_FriendRequests_UpdateStatus", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new FriendRequestBasic
                {
                    RequestId = reader.GetInt32(reader.GetOrdinal("RequestId")),
                    RequesterEmail = reader.GetString(reader.GetOrdinal("RequesterEmail")),
                    AddresseeEmail = reader.GetString(reader.GetOrdinal("AddresseeEmail")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    RequestedAt = reader.GetDateTime(reader.GetOrdinal("RequestedAt")),
                    RespondedAt = reader.IsDBNull(reader.GetOrdinal("RespondedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("RespondedAt"))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // =====================================================
    // FRIENDSHIPS
    // =====================================================

    // Returns true if there's an active friendship between the two emails.
    public bool FriendshipExists(string email1, string email2)
    {
        var (e1, e2) = NormalizePair(email1, email2);

        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email1", e1);
        paramDic.Add("@Email2", e2);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Friendships_ExistsPair", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the user's accepted friendships with friend names.
    public List<FriendshipRow> GetFriendshipsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Friendships_GetByUser", con, paramDic);

        try
        {
            List<FriendshipRow> list = new List<FriendshipRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new FriendshipRow
                {
                    FriendshipId = reader.GetInt32(reader.GetOrdinal("FriendshipId")),
                    Email1 = reader.GetString(reader.GetOrdinal("Email1")),
                    Email2 = reader.GetString(reader.GetOrdinal("Email2")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    IsActive = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsActive"))),
                    FriendEmail = reader.GetString(reader.GetOrdinal("FriendEmail")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Inserts a Friendship row after both sides accept; returns its ID.
    public int CreateFriendship(string email1, string email2)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email1", email1);
        paramDic.Add("@Email2", email2);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Friendships_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Soft-deletes a friendship if the user is one of the two parties.
    public bool DeactivateFriendship(int friendshipId, string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@FriendshipId", friendshipId);
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Friendships_Deactivate", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns a friendship by ID only if the user is one of the two parties.
    public FriendshipBasic? GetFriendshipForUser(int friendshipId, string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@FriendshipId", friendshipId);
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Collaboration_GetFriendshipForUser", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new FriendshipBasic
                {
                    FriendshipId = reader.GetInt32(reader.GetOrdinal("FriendshipId")),
                    Email1 = reader.GetString(reader.GetOrdinal("Email1")),
                    Email2 = reader.GetString(reader.GetOrdinal("Email2")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    IsActive = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsActive")))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the two emails ordered alphabetically (Email1 < Email2).
    public static (string, string) NormalizePair(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? (a, b) : (b, a);

    // =====================================================
    // SHARED TASKS (full CRUD)
    // =====================================================

    // Returns flat join rows for every shared task the user is a member of.
    public List<SharedTaskFullRow> GetSharedTasksByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_GetByUser", con, paramDic);

        try
        {
            List<SharedTaskFullRow> list = new List<SharedTaskFullRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapSharedTaskFullRow(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns flat join rows for one shared task (one row per member).
    public List<SharedTaskFullRow> GetSharedTaskByTaskId(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_GetByTaskId", con, paramDic);

        try
        {
            List<SharedTaskFullRow> list = new List<SharedTaskFullRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapSharedTaskFullRow(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true if the task is already shared.
    public bool SharedTaskExists(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_ExistsByTaskId", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Updates a single member's response status; returns true if a row changed.
    public bool UpdateSharedTaskMemberStatus(int taskId, string email, string responseStatus)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Email", email);
        paramDic.Add("@ResponseStatus", responseStatus);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTaskMembers_UpdateStatus", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true when every member's status is Accepted.
    public bool AllSharedTaskMembersAccepted(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTaskMembers_AllAccepted", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the list of every member's email for a shared task.
    public List<string> GetSharedTaskMemberEmails(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTaskMembers_GetEmails", con, paramDic);

        try
        {
            List<string> list = new List<string>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetString(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Stores the partner's copy-task ID on a shared-task member row.
    public void UpdateSharedTaskMemberCopyTaskId(int taskId, string email, int copyTaskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Email", email);
        paramDic.Add("@CopyTaskId", copyTaskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTaskMembers_UpdateCopyTaskId", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns the partner's copy-task ID for a shared-task member.
    public int? GetSharedTaskMemberCopyTaskId(int taskId, string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTaskMembers_GetCopyTaskId", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return null;
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Removes the partner-side task copies after a shared task is cancelled.
    public int CleanupSharedTaskPartnerCopies(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_SharedTasks_CleanupPartnerCopies", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return 0;
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a SharedTaskFullRow record.
    private static SharedTaskFullRow MapSharedTaskFullRow(SqlDataReader r)
    {
        return new SharedTaskFullRow
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            TaskTitle = r.GetString(r.GetOrdinal("TaskTitle")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.IsDBNull(r.GetOrdinal("CourseName")) ? null : r.GetString(r.GetOrdinal("CourseName")),
            CreatedByEmail = r.GetString(r.GetOrdinal("CreatedByEmail")),
            CreatorFirstName = r.GetString(r.GetOrdinal("CreatorFirstName")),
            CreatorLastName = r.GetString(r.GetOrdinal("CreatorLastName")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
            SharedStatus = r.GetString(r.GetOrdinal("SharedStatus")),
            MemberEmail = r.GetString(r.GetOrdinal("MemberEmail")),
            MemberFirstName = r.GetString(r.GetOrdinal("MemberFirstName")),
            MemberLastName = r.GetString(r.GetOrdinal("MemberLastName")),
            ResponseStatus = r.GetString(r.GetOrdinal("ResponseStatus")),
            RespondedAt = r.IsDBNull(r.GetOrdinal("RespondedAt")) ? null : r.GetDateTime(r.GetOrdinal("RespondedAt"))
        };
    }

    // =====================================================
    // COLLABORATION
    // =====================================================

    // Marks the user's enrollment as approved for course-level task sharing.
    public bool SetCourseShareApproved(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_SetCourseShareApproved", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Lists pending shared-task memberships for this course.
    public List<PendingMemberForCourseRow> GetPendingMembersForCourse(string email, int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Collaboration_GetPendingMembersForCourse", con, paramDic);

        try
        {
            List<PendingMemberForCourseRow> list = new List<PendingMemberForCourseRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new PendingMemberForCourseRow
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    ResponseStatus = reader.GetString(reader.GetOrdinal("ResponseStatus")),
                    RespondedAt = reader.IsDBNull(reader.GetOrdinal("RespondedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("RespondedAt")),
                    CreatedByEmail = reader.GetString(reader.GetOrdinal("CreatedByEmail")),
                    SharedStatus = reader.GetString(reader.GetOrdinal("SharedStatus"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // =====================================================
    // NOTIFICATIONS
    // =====================================================

    // Inserts a generic notification row and returns its ID.
    public int CreateNotification(string email, string type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@Type", type);
        paramDic.Add("@Title", title);
        paramDic.Add("@Message", message);
        paramDic.Add("@RelatedEntityId", (object?)relatedEntityId ?? DBNull.Value);
        paramDic.Add("@RelatedEntityType", (object?)relatedEntityType ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    public (List<NotificationRow> Notifications, int UnreadCount) GetNotificationsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_GetByUser", con, paramDic);

        try
        {
            var notifications = new List<NotificationRow>();
            int unreadCount = 0;

            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            // Result set 1: notifications
            while (reader.Read())
            {
                notifications.Add(MapNotificationRow(reader));
            }
            // Result set 2: unread count
            if (reader.NextResult() && reader.Read())
            {
                unreadCount = reader.GetInt32(0);
            }
            return (notifications, unreadCount);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns just the unread notification count for badge polling.
    public int GetUnreadNotificationCount(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_GetUnreadCount", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Marks the specified notifications as read.
    public void MarkNotificationsRead(string email, List<int> notificationIds)
    {
        var ids = string.Join(",", notificationIds);

        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@NotificationIds", ids);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_MarkRead", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Marks every one of the user's notifications as read.
    public void MarkAllNotificationsRead(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_MarkAllRead", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true when a similar notification already exists for the user today.
    public bool IsNotificationDuplicate(string email, string type, int? relatedEntityId = null, string? relatedEntityType = null, int sinceHours = 24)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@Type", type);
        paramDic.Add("@RelatedEntityId", (object?)relatedEntityId ?? DBNull.Value);
        paramDic.Add("@RelatedEntityType", (object?)relatedEntityType ?? DBNull.Value);
        paramDic.Add("@SinceHours", sinceHours);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_IsDuplicate", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true if the user has any unread notification of the given type.
    public bool HasUnreadNotificationOfType(string email, string type)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@Type", type);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_HasUnreadByType", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns tasks whose due date falls inside the next 24 hours.
    public List<UpcomingDeadlineTask> GetUpcomingDeadlineTasks(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_GetUpcomingDeadlineTasks", con, paramDic);

        try
        {
            List<UpcomingDeadlineTask> list = new List<UpcomingDeadlineTask>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new UpcomingDeadlineTask
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns today's tasks for the daily morning summary notification.
    public List<DailySummaryTask> GetDailySummaryData(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_GetDailySummaryData", con, paramDic);

        try
        {
            List<DailySummaryTask> list = new List<DailySummaryTask>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new DailySummaryTask
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    public (int TaskCount, int ExamCount) GetWeeklyPlanData(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_GetWeeklyPlanData", con, paramDic);

        try
        {
            int taskCount = 0;
            int examCount = 0;

            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                taskCount = reader.GetInt32(0);
            if (reader.NextResult() && reader.Read())
                examCount = reader.GetInt32(0);

            return (taskCount, examCount);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Returns true if a weekly reminder was already sent in the last 6 days.
    public bool HasRecentWeeklyReminder(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Notifications_HasRecentWeekly", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // Reads a SqlDataReader row into a NotificationRow record.
    private static NotificationRow MapNotificationRow(SqlDataReader r)
    {
        return new NotificationRow
        {
            NotificationId = r.GetInt32(r.GetOrdinal("NotificationId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            Type = r.GetString(r.GetOrdinal("Type")),
            Title = r.GetString(r.GetOrdinal("Title")),
            Message = r.GetString(r.GetOrdinal("Message")),
            IsRead = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsRead"))),
            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
            RelatedEntityId = r.IsDBNull(r.GetOrdinal("RelatedEntityId")) ? null : r.GetInt32(r.GetOrdinal("RelatedEntityId")),
            RelatedEntityType = r.IsDBNull(r.GetOrdinal("RelatedEntityType")) ? null : r.GetString(r.GetOrdinal("RelatedEntityType"))
        };
    }

    // =====================================================
    // RECOVERED METHODS (HW3 inline sync)
    // =====================================================

    // Reads a SqlDataReader row into a SchedulingTaskRow record.
    private static SchedulingTaskRow MapSchedulingTaskRow(SqlDataReader r)
    {
        return new SchedulingTaskRow
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            Title = r.GetString(r.GetOrdinal("Title")),
            Type = r.GetString(r.GetOrdinal("Type")),
            EstimatedHours = r.IsDBNull(r.GetOrdinal("EstimatedHours")) ? null : r.GetDecimal(r.GetOrdinal("EstimatedHours")),
            DueDate = r.IsDBNull(r.GetOrdinal("DueDate")) ? null : r.GetDateTime(r.GetOrdinal("DueDate")),
            IsCompleted = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsCompleted"))),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            AllowSplitting = Convert.ToBoolean(r.GetValue(r.GetOrdinal("AllowSplitting"))),
            IsManuallyPinned = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsManuallyPinned"))),
            IsManualPriority = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsManualPriority"))),
            Email = r.GetString(r.GetOrdinal("Email")),
            CourseCredits = r.IsDBNull(r.GetOrdinal("CourseCredits")) ? null : r.GetDecimal(r.GetOrdinal("CourseCredits")),
            DefaultTaskEstimatedHours = r.IsDBNull(r.GetOrdinal("DefaultTaskEstimatedHours")) ? null : r.GetDouble(r.GetOrdinal("DefaultTaskEstimatedHours")),
            CourseExamPrepHoursPerDay = r.IsDBNull(r.GetOrdinal("CourseExamPrepHoursPerDay")) ? null : r.GetDouble(r.GetOrdinal("CourseExamPrepHoursPerDay")),
            CourseExamPrepDays = r.IsDBNull(r.GetOrdinal("CourseExamPrepDays")) ? null : r.GetInt32(r.GetOrdinal("CourseExamPrepDays")),
            HasSharedTask = Convert.ToBoolean(r.GetValue(r.GetOrdinal("HasSharedTask")))
        };
    }

    // Reads a SqlDataReader row into a TaskEventRow record.
    private static TaskEventRow MapTaskEventRow(SqlDataReader r)
    {
        return new TaskEventRow
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Recurring = HasColumn(r, "Recurring") && !r.IsDBNull(r.GetOrdinal("Recurring"))
                && Convert.ToBoolean(r.GetValue(r.GetOrdinal("Recurring"))),
            RecurrenceEndDate = HasColumn(r, "RecurrenceEndDate") && !r.IsDBNull(r.GetOrdinal("RecurrenceEndDate"))
                ? r.GetDateTime(r.GetOrdinal("RecurrenceEndDate")) : (DateTime?)null,
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            Status = r.IsDBNull(r.GetOrdinal("Status")) ? null : r.GetString(r.GetOrdinal("Status"))
        };
    }

    // Reads a SqlDataReader row into a SimpleTaskRow record.
    private static SimpleTaskRow MapSimpleTaskRow(SqlDataReader r)
    {
        return new SimpleTaskRow
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            Title = r.GetString(r.GetOrdinal("Title")),
            Type = r.GetString(r.GetOrdinal("Type")),
            EstimatedHours = r.IsDBNull(r.GetOrdinal("EstimatedHours")) ? null : r.GetDecimal(r.GetOrdinal("EstimatedHours")),
            DueDate = r.IsDBNull(r.GetOrdinal("DueDate")) ? null : r.GetDateTime(r.GetOrdinal("DueDate")),
            IsCompleted = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsCompleted"))),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            AllowSplitting = Convert.ToBoolean(r.GetValue(r.GetOrdinal("AllowSplitting"))),
            Email = r.GetString(r.GetOrdinal("Email"))
        };
    }

    // 1. ApproveTaskEvents
    public (int ApprovedCount, int RemovedPast) ApproveTaskEvents(int taskId, string email, DateTime now)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Email", email);
        paramDic.Add("@Now", now);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_Approve", con, paramDic);

        try
        {
            int approved = 0, removed = 0;
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                approved = reader.GetInt32(reader.GetOrdinal("ApprovedCount"));
                removed = reader.GetInt32(reader.GetOrdinal("RemovedPast"));
            }
            return (approved, removed);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 2. ClassEventExists
    public bool ClassEventExists(string email, int courseId, DateTime from, DateTime to)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_ClassEvents_Exists", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 3. ClearLastCalendarSync
    public void ClearLastCalendarSync(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_ClearLastCalendarSync", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 4. ClearMoodle
    public void ClearMoodle(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_ClearMoodle", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 5. CountGcalEvents
    public int CountGcalEvents(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_PersonalEvents_CountGcal", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 6. CourseExists
    public bool CourseExists(int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_Exists", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 7. CreateInstructor
    public int CreateInstructor(string name)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Name", name);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Instructors_Create", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 8. CreateTaskWithMoodleId
    public int CreateTaskWithMoodleId(int courseId, string email, string title, string type,
        decimal? estimatedHours, DateTime? dueDate, string? priority, string? moodleId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@Email", email);
        paramDic.Add("@Title", title);
        paramDic.Add("@Type", type);
        paramDic.Add("@EstimatedHours", (object?)estimatedHours ?? DBNull.Value);
        paramDic.Add("@DueDate", (object?)dueDate ?? DBNull.Value);
        paramDic.Add("@Priority", (object?)priority ?? DBNull.Value);
        paramDic.Add("@MoodleId", (object?)moodleId ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_CreateWithMoodleId", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 9. DeleteCourse
    public void DeleteCourse(int courseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_Delete", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 10. DeleteEventById
    public void DeleteEventById(int eventId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_DeleteById", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 11. DeleteTaskWithEvents
    public void DeleteTaskWithEvents(int taskId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_DeleteWithEvents", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 12. DisconnectGoogleCalendar
    public void DisconnectGoogleCalendar(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_DisconnectGoogleCalendar", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 13. FindExamByCourseAndSession
    public ExamBasicRow? FindExamByCourseAndSession(string email, int courseId, string session)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@Session", session);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_FindByCourseAndSession", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new ExamBasicRow
                {
                    ExamId = reader.GetInt32(reader.GetOrdinal("ExamId")),
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                    Time = reader.GetTimeSpan(reader.GetOrdinal("Time")),
                    Session = reader.GetString(reader.GetOrdinal("Session")),
                    Duration = reader.IsDBNull(reader.GetOrdinal("Duration")) ? null : reader.GetInt32(reader.GetOrdinal("Duration")),
                    IsTakingExam = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsTakingExam")))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 14. FindInstructorByName
    public Instructor? FindInstructorByName(string name)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Name", name);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Instructors_FindByName", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new Instructor
                {
                    InstructorId = reader.GetInt32(reader.GetOrdinal("InstructorId")),
                    InstructorName = reader.GetString(reader.GetOrdinal("InstructorName"))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 15. FindPersonalEventByGcalId
    public PersonalEventRow? FindPersonalEventByGcalId(string email, string gcalMarker)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@GcalMarker", gcalMarker);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_PersonalEvents_FindByGcalId", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new PersonalEventRow
                {
                    EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                    From = reader.GetDateTime(reader.GetOrdinal("From")),
                    To = reader.GetDateTime(reader.GetOrdinal("To")),
                    Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? null : reader.GetString(reader.GetOrdinal("Type")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 16. FindTaskByMatch
    public SimpleTaskRow? FindTaskByMatch(string email, string title, int courseId, DateTime? dueDate)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@Title", title);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@DueDate", (object?)dueDate ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_FindByMatch", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapSimpleTaskRow(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 17. FindTaskByMoodleId
    public MoodleTaskMatch? FindTaskByMoodleId(string email, string moodleId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@MoodleId", moodleId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_FindByMoodleId", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
            {
                return new MoodleTaskMatch
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? null : reader.GetDateTime(reader.GetOrdinal("DueDate")),
                    IsCompleted = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsCompleted")))
                };
            }
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 18. GetAllCourses
    public List<Course> GetAllCourses()
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_GetAll", con, paramDic);

        try
        {
            List<Course> list = new List<Course>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapCourse(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 19. GetAllIncompleteTasks
    public List<SchedulingTaskRow> GetAllIncompleteTasks(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetAllIncomplete", con, paramDic);

        try
        {
            List<SchedulingTaskRow> list = new List<SchedulingTaskRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                var row = MapSchedulingTaskRow(reader);
                row.SubTaskCount = reader.GetInt32(reader.GetOrdinal("SubTaskCount"));
                row.TaskEventCount = reader.GetInt32(reader.GetOrdinal("TaskEventCount"));
                row.HasNeedReview = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("HasNeedReview")));
                list.Add(row);
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 20. GetAllUserEmails
    public List<string> GetAllUserEmails()
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_GetAllEmails", con, paramDic);

        try
        {
            List<string> list = new List<string>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetString(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 21. GetBaseEventsInRangeOrRecurring
    public List<Event> GetBaseEventsInRangeOrRecurring(string email, DateTime rangeStart, DateTime rangeEnd)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@RangeStart", rangeStart);
        paramDic.Add("@RangeEnd", rangeEnd);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_GetBaseInRangeOrRecurring", con, paramDic);

        try
        {
            List<Event> list = new List<Event>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new Event
                {
                    EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    From = reader.GetDateTime(reader.GetOrdinal("From")),
                    To = reader.GetDateTime(reader.GetOrdinal("To")),
                    Recurring = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("Recurring"))),
                    RecurrenceEndDate = reader.IsDBNull(reader.GetOrdinal("RecurrenceEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("RecurrenceEndDate"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 22. GetClassEventIdsByUser
    public List<int> GetClassEventIdsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_ClassEvents_GetIdsByUser", con, paramDic);

        try
        {
            List<int> list = new List<int>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetInt32(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 23. GetCompletedTasksForML
    public List<MLCompletedTaskRow> GetCompletedTasksForML(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetCompletedForML", con, paramDic);

        try
        {
            List<MLCompletedTaskRow> list = new List<MLCompletedTaskRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new MLCompletedTaskRow
                {
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    ActualHours = reader.GetDouble(reader.GetOrdinal("ActualHours")),
                    EstimatedHours = reader.GetDouble(reader.GetOrdinal("EstimatedHours"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 24. GetEventsInDateRange
    public List<Event> GetEventsInDateRange(string email, DateTime from, DateTime to)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_GetInDateRange", con, paramDic);

        try
        {
            List<Event> list = new List<Event>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new Event
                {
                    EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    From = reader.GetDateTime(reader.GetOrdinal("From")),
                    To = reader.GetDateTime(reader.GetOrdinal("To")),
                    Recurring = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("Recurring"))),
                    RecurrenceEndDate = reader.IsDBNull(reader.GetOrdinal("RecurrenceEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("RecurrenceEndDate"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 25. GetExamsForScheduling
    public List<SchedulingExamRow> GetExamsForScheduling(string email, DateTime rangeStart, DateTime rangeEnd)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@RangeStart", rangeStart);
        paramDic.Add("@RangeEnd", rangeEnd);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_GetForScheduling", con, paramDic);

        try
        {
            List<SchedulingExamRow> list = new List<SchedulingExamRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new SchedulingExamRow
                {
                    ExamId = reader.GetInt32(reader.GetOrdinal("ExamId")),
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                    Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                    Time = reader.GetTimeSpan(reader.GetOrdinal("Time")),
                    Session = reader.GetString(reader.GetOrdinal("Session")),
                    Duration = reader.IsDBNull(reader.GetOrdinal("Duration")) ? null : reader.GetInt32(reader.GetOrdinal("Duration")),
                    IsTakingExam = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsTakingExam"))),
                    CourseExamPrepHoursPerDay = reader.IsDBNull(reader.GetOrdinal("CourseExamPrepHoursPerDay")) ? null : reader.GetDouble(reader.GetOrdinal("CourseExamPrepHoursPerDay")),
                    CourseExamPrepDays = reader.IsDBNull(reader.GetOrdinal("CourseExamPrepDays")) ? null : reader.GetInt32(reader.GetOrdinal("CourseExamPrepDays"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 26. GetGcalPersonalEventIds
    public List<int> GetGcalPersonalEventIds(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_PersonalEvents_GetGcalEventIds", con, paramDic);

        try
        {
            List<int> list = new List<int>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetInt32(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 27. GetIncompleteLeafTasks
    public List<SchedulingTaskRow> GetIncompleteLeafTasks(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetIncompleteLeaf", con, paramDic);

        try
        {
            List<SchedulingTaskRow> list = new List<SchedulingTaskRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapSchedulingTaskRow(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 28. GetIncompleteTasksWithEvents
    public (List<DashboardTaskRow> Tasks, List<DashboardTaskEventRow> TaskEvents) GetIncompleteTasksWithEvents(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetIncompleteWithEvents", con, paramDic);

        try
        {
            var tasks = new List<DashboardTaskRow>();
            var taskEvents = new List<DashboardTaskEventRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                tasks.Add(new DashboardTaskRow
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Type = reader.GetString(reader.GetOrdinal("Type")),
                    EstimatedHours = reader.IsDBNull(reader.GetOrdinal("EstimatedHours")) ? null : reader.GetDecimal(reader.GetOrdinal("EstimatedHours")),
                    DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? null : reader.GetDateTime(reader.GetOrdinal("DueDate")),
                    IsCompleted = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsCompleted"))),
                    Priority = reader.IsDBNull(reader.GetOrdinal("Priority")) ? null : reader.GetString(reader.GetOrdinal("Priority")),
                    AllowSplitting = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("AllowSplitting"))),
                    IsManuallyPinned = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsManuallyPinned"))),
                    IsManualPriority = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsManualPriority"))),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CourseCredits = reader.IsDBNull(reader.GetOrdinal("CourseCredits")) ? null : reader.GetDecimal(reader.GetOrdinal("CourseCredits")),
                    DefaultTaskEstimatedHours = reader.IsDBNull(reader.GetOrdinal("DefaultTaskEstimatedHours")) ? null : reader.GetDouble(reader.GetOrdinal("DefaultTaskEstimatedHours")),
                    SharedStatus = reader.IsDBNull(reader.GetOrdinal("SharedStatus")) ? null : reader.GetString(reader.GetOrdinal("SharedStatus"))
                });
            }
            if (reader.NextResult())
            {
                while (reader.Read())
                {
                    taskEvents.Add(new DashboardTaskEventRow
                    {
                        TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                        EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                        From = reader.GetDateTime(reader.GetOrdinal("From")),
                        To = reader.GetDateTime(reader.GetOrdinal("To")),
                        Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? null : reader.GetString(reader.GetOrdinal("Status"))
                    });
                }
            }
            return (tasks, taskEvents);
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 29. GetNeedReviewTaskIds
    public List<int> GetNeedReviewTaskIds(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_GetNeedReviewTaskIds", con, paramDic);

        try
        {
            List<int> list = new List<int>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetInt32(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 30. GetOrphanedStudyTaskIds
    public List<int> GetOrphanedStudyTaskIds(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetOrphanedStudyTasks", con, paramDic);

        try
        {
            List<int> list = new List<int>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetInt32(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 31. GetPersonalEventsByUser
    public List<PersonalEventRow> GetPersonalEventsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_PersonalEvents_GetByUser", con, paramDic);

        try
        {
            List<PersonalEventRow> list = new List<PersonalEventRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new PersonalEventRow
                {
                    EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    From = reader.GetDateTime(reader.GetOrdinal("From")),
                    To = reader.GetDateTime(reader.GetOrdinal("To")),
                    Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? null : reader.GetString(reader.GetOrdinal("Type")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 32. GetPinnedTaskIds
    public List<int> GetPinnedTaskIds(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetPinnedIds", con, paramDic);

        try
        {
            List<int> list = new List<int>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetInt32(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 33. GetStudyForExamTask
    public SimpleTaskRow? GetStudyForExamTask(string email, int courseId, DateTime dueDate)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@DueDate", dueDate);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_GetStudyForExam", con, paramDic);

        try
        {
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            if (reader.Read())
                return MapSimpleTaskRow(reader);
            return null;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 34. GetTaskEventsByTaskIdsAndStatuses
    public List<TaskEventRow> GetTaskEventsByTaskIdsAndStatuses(int taskId1, int taskId2)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId1", taskId1);
        paramDic.Add("@TaskId2", taskId2);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_GetByTaskIdsAndStatuses", con, paramDic);

        try
        {
            List<TaskEventRow> list = new List<TaskEventRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTaskEventRow(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 35. GetTaskEventsByUserAndStatus
    public List<TaskEventRow> GetTaskEventsByUserAndStatus(string email, string status1, string? status2 = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@Status1", status1);
        paramDic.Add("@Status2", (object?)status2 ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_GetByUserAndStatus", con, paramDic);

        try
        {
            List<TaskEventRow> list = new List<TaskEventRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTaskEventRow(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 36. GetTaskEventsInRange
    public List<TaskEventRow> GetTaskEventsInRange(string email, DateTime from, DateTime to, string? status = null)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);
        paramDic.Add("@Status", (object?)status ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_TaskEvents_GetByUserInRange", con, paramDic);

        try
        {
            List<TaskEventRow> list = new List<TaskEventRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(MapTaskEventRow(reader));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 37. GetUpcomingExams
    public List<SchedulingExamRow> GetUpcomingExams(string email, DateTime fromDate)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@FromDate", fromDate);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_GetUpcoming", con, paramDic);

        try
        {
            List<SchedulingExamRow> list = new List<SchedulingExamRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new SchedulingExamRow
                {
                    ExamId = reader.GetInt32(reader.GetOrdinal("ExamId")),
                    CourseId = reader.GetInt32(reader.GetOrdinal("CourseId")),
                    CourseName = reader.GetString(reader.GetOrdinal("CourseName")),
                    Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                    Time = reader.GetTimeSpan(reader.GetOrdinal("Time")),
                    Session = reader.GetString(reader.GetOrdinal("Session")),
                    Duration = reader.IsDBNull(reader.GetOrdinal("Duration")) ? null : reader.GetInt32(reader.GetOrdinal("Duration")),
                    IsTakingExam = Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("IsTakingExam")))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 38. GetUserCoursesWithName
    public List<(int CourseId, string CourseName)> GetUserCoursesWithName(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_GetWithCourseName", con, paramDic);

        try
        {
            List<(int CourseId, string CourseName)> list = new List<(int, string)>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add((reader.GetInt32(reader.GetOrdinal("CourseId")), reader.GetString(reader.GetOrdinal("CourseName"))));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 39. GetUsersByComposioId
    public List<string> GetUsersByComposioId(string composioId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ComposioConnectedAccountId", composioId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_GetByComposioId", con, paramDic);

        try
        {
            List<string> list = new List<string>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetString(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 40. GetUsersForMoodleSync
    public List<string> GetUsersForMoodleSync(DateTime cutoff)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Cutoff", cutoff);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_GetForMoodleSync", con, paramDic);

        try
        {
            List<string> list = new List<string>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetString(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 41. GetUsersForRuppinetSync
    public List<string> GetUsersForRuppinetSync(DateTime cutoff)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Cutoff", cutoff);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_GetForRuppinetSync", con, paramDic);

        try
        {
            List<string> list = new List<string>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
                list.Add(reader.GetString(0));
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 42. GetWorkEventsByUser
    public List<WorkEventRow> GetWorkEventsByUser(string email)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_WorkEvents_GetByUser", con, paramDic);

        try
        {
            List<WorkEventRow> list = new List<WorkEventRow>();
            SqlDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
            while (reader.Read())
            {
                list.Add(new WorkEventRow
                {
                    EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    From = reader.GetDateTime(reader.GetOrdinal("From")),
                    To = reader.GetDateTime(reader.GetOrdinal("To")),
                    WorkPlace = reader.IsDBNull(reader.GetOrdinal("WorkPlace")) ? null : reader.GetString(reader.GetOrdinal("WorkPlace")),
                    TravelTime = reader.IsDBNull(reader.GetOrdinal("TravelTime")) ? null : reader.GetInt32(reader.GetOrdinal("TravelTime"))
                });
            }
            return list;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 43. OtherUsersEnrolled
    public bool OtherUsersEnrolled(int courseId, string excludeEmail)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@ExcludeEmail", excludeEmail);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_UserCourses_OtherUsersExist", con, paramDic);

        try
        {
            object result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) == 1;
        }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 44. ReassignClassEventsCourse
    public void ReassignClassEventsCourse(string email, int fromCourseId, int toCourseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@FromCourseId", fromCourseId);
        paramDic.Add("@ToCourseId", toCourseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_ClassEvents_ReassignCourse", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 45. ReassignExamsCourse
    public void ReassignExamsCourse(int fromCourseId, int toCourseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@FromCourseId", fromCourseId);
        paramDic.Add("@ToCourseId", toCourseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_ReassignCourse", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 46. ReassignTasksCourse
    public void ReassignTasksCourse(string email, int fromCourseId, int toCourseId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@FromCourseId", fromCourseId);
        paramDic.Add("@ToCourseId", toCourseId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_ReassignCourse", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 47. UpdateComposioId
    public void UpdateComposioId(string email, string? composioId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@ComposioConnectedAccountId", (object?)composioId ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateComposioId", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 48. UpdateCourseInstructor
    public void UpdateCourseInstructor(int courseId, int instructorId)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@CourseId", courseId);
        paramDic.Add("@InstructorId", instructorId);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Courses_UpdateInstructor", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 49. UpdateEventTimes
    public void UpdateEventTimes(int eventId, DateTime from, DateTime to)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@EventId", eventId);
        paramDic.Add("@From", from);
        paramDic.Add("@To", to);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Events_UpdateTimes", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 50. UpdateExamFull
    public void UpdateExamFull(int examId, DateTime date, TimeSpan time, int? duration)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@ExamId", examId);
        paramDic.Add("@Date", date);
        paramDic.Add("@Duration", (object?)duration ?? DBNull.Value);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Exams_UpdateFull", con, paramDic);
        // @Time requires explicit SqlDbType.Time
        cmd.Parameters.Add(new SqlParameter("@Time", SqlDbType.Time) { Value = time });

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 51. UpdateGoogleToken
    public void UpdateGoogleToken(string email, string accessToken, DateTime lastSync)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@GoogleCalendarAccessToken", accessToken);
        paramDic.Add("@LastCalendarSync", lastSync);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateGoogleToken", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 52. UpdateLastCalendarSync
    public void UpdateLastCalendarSync(string email, DateTime lastSync)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@LastCalendarSync", lastSync);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateLastCalendarSync", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 53. UpdateLastMoodleSync
    public void UpdateLastMoodleSync(string email, DateTime lastSync)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@LastMoodleSync", lastSync);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateLastMoodleSync", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 54. UpdateLastRuppinetSync
    public void UpdateLastRuppinetSync(string email, DateTime lastSync)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@Email", email);
        paramDic.Add("@LastRuppinetSync", lastSync);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Users_UpdateLastRuppinetSync", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }

    // 55. UpdateTaskPriority
    public void UpdateTaskPriority(int taskId, string priority)
    {
        SqlConnection con;
        SqlCommand cmd;
        try { con = connect("SmartStudyDb"); }
        catch (Exception) { throw; }

        Dictionary<string, object> paramDic = new Dictionary<string, object>();
        paramDic.Add("@TaskId", taskId);
        paramDic.Add("@Priority", priority);

        cmd = CreateCommandWithStoredProcedureGeneral("SS_Tasks_UpdatePriority", con, paramDic);

        try { cmd.ExecuteNonQuery(); }
        catch (Exception) { throw; }
        finally { if (con != null) con.Close(); }
    }
}

// DTO class for Moodle task match result
public class MoodleTaskMatch
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
}
// Course row joined with the user's enrollment metadata.
public class CourseWithEnrollment
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public decimal? WeeklyHours { get; set; }
    public decimal? Credits { get; set; }
    public string? Semester { get; set; }
    public int? InstructorId { get; set; }
    public string? InstructorName { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
    public string? StudyPartnerEmail { get; set; }
    public bool SharedByDefault { get; set; }
    public bool CourseShareApproved { get; set; }
    public int TaskCount { get; set; }
    public int ExamCount { get; set; }
}

// Exam row joined with course name and prep-config fields.
public class ExamWithCourse
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
    public bool IsTakingExam { get; set; }
    public double? ExamPrepHoursPerDay { get; set; }
    public int? ExamPrepDays { get; set; }
}

// Task row joined with course name and per-course prep config.
public class TaskWithCourse
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public int? ParentTaskId { get; set; }
    public bool AllowSplitting { get; set; }
    public bool IsManuallyPinned { get; set; }
    public bool IsManualPriority { get; set; }
    public string Email { get; set; } = null!;
}

// Compact task-event projection used for scheduling/sharing logic.
public class TaskEventInfo
{
    public int EventId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public string? Status { get; set; }
}

// Aggregate sharing record returned alongside a shared task.
public class SharedTaskInfo
{
    public int TaskId { get; set; }
    public string SharedStatus { get; set; } = null!;
    public string CreatedByEmail { get; set; } = null!;
    public List<SharedTaskMemberInfo> Members { get; set; } = new();
}

// Single member entry inside a SharedTaskInfo.
public class SharedTaskMemberInfo
{
    public string Email { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public string FullName { get; set; } = null!;
}

// Estimated/actual hour pair for one completed task.
public class MLDataRow
{
    public decimal ActualHours { get; set; }
    public decimal EstimatedHours { get; set; }
}

// Per-course estimation-accuracy summary.
public class MLInsightRow
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public int TaskCount { get; set; }
    public double AvgEstimated { get; set; }
    public double AvgActual { get; set; }
    public double Accuracy { get; set; }
}

// Friend Request DTOs
public class FriendRequestRow
{
    public int RequestId { get; set; }
    public string RequesterEmail { get; set; } = null!;
    public string AddresseeEmail { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string FriendEmail { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}

// Minimal friend-request row used after status updates.
public class FriendRequestBasic
{
    public int RequestId { get; set; }
    public string RequesterEmail { get; set; } = null!;
    public string AddresseeEmail { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

// Friendship DTOs
public class FriendshipRow
{
    public int FriendshipId { get; set; }
    public string Email1 { get; set; } = null!;
    public string Email2 { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string FriendEmail { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}

// Minimal friendship row used to authorize collaboration actions.
public class FriendshipBasic
{
    public int FriendshipId { get; set; }
    public string Email1 { get; set; } = null!;
    public string Email2 { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

// Shared Task DTOs
public class SharedTaskFullRow
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = null!;
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public string CreatedByEmail { get; set; } = null!;
    public string CreatorFirstName { get; set; } = null!;
    public string CreatorLastName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string SharedStatus { get; set; } = null!;
    public string MemberEmail { get; set; } = null!;
    public string MemberFirstName { get; set; } = null!;
    public string MemberLastName { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
}


// Pending shared-task membership awaiting course-share approval.
public class PendingMemberForCourseRow
{
    public int TaskId { get; set; }
    public string Email { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
    public string CreatedByEmail { get; set; } = null!;
    public string SharedStatus { get; set; } = null!;
}

// Subtype-aware projection of an event with all subtype fields flattened.
public class TypedEvent
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public string EventType { get; set; } = "unknown";

    // ClassEvent fields
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public string? Location { get; set; }
    public decimal? Duration { get; set; }

    // TaskEvent fields
    public int? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public string? Status { get; set; }
    public bool? IsManuallyPinned { get; set; }
    public bool IsShared { get; set; }
    public string? SharedStatus { get; set; }

    // WorkEvent fields
    public int? TravelTime { get; set; }
    public string? WorkPlace { get; set; }

    // PersonalEvent fields
    public string? Type { get; set; }
    public string? Description { get; set; }
}

// Notification DTOs
public class NotificationRow
{
    public int NotificationId { get; set; }
    public string Email { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

// Task projection for deadline-notification generation.
public class UpcomingDeadlineTask
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string CourseName { get; set; } = null!;
}

// Task projection for the daily morning summary notification.
public class DailySummaryTask
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string CourseName { get; set; } = null!;
}

// Scheduling/Stress service DTOs
public class SchedulingTaskRow
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public bool AllowSplitting { get; set; }
    public bool IsManuallyPinned { get; set; }
    public bool IsManualPriority { get; set; }
    public string Email { get; set; } = null!;
    public decimal? CourseCredits { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public double? CourseExamPrepHoursPerDay { get; set; }
    public int? CourseExamPrepDays { get; set; }
    public bool HasSharedTask { get; set; }
    // Only populated by GetAllIncomplete
    public int SubTaskCount { get; set; }
    public int TaskEventCount { get; set; }
    public bool HasNeedReview { get; set; }
}

// Task-event projection consumed by the auto-scheduling engine.
public class TaskEventRow
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Recurring { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int TaskId { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
}

// Simple (From, To) pair returned by GetEventTimeRange.
public class EventTimeRange
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

// Compact task row used by sync helpers.
public class SimpleTaskRow
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public bool AllowSplitting { get; set; }
    public string Email { get; set; } = null!;
}

// Exam projection consumed by the auto-scheduling engine.
public class SchedulingExamRow
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
    public bool IsTakingExam { get; set; }
    public double? CourseExamPrepHoursPerDay { get; set; }
    public int? CourseExamPrepDays { get; set; }
}

// Completed-task projection used to learn per-course time bias.
public class MLCompletedTaskRow
{
    public int CourseId { get; set; }
    public double ActualHours { get; set; }
    public double EstimatedHours { get; set; }
}

// Work event row with workplace and travel time.
public class WorkEventRow
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? WorkPlace { get; set; }
    public int? TravelTime { get; set; }
}

// Personal event row with type and description.
public class PersonalEventRow
{
    public int EventId { get; set; }
    public string? Email { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}

// Minimal exam row used by sync upsert logic.
public class ExamBasicRow
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public string Session { get; set; } = null!;
    public int? Duration { get; set; }
    public bool IsTakingExam { get; set; }
}

// Task projection consumed by the dashboard aggregator.
public class DashboardTaskRow
{
    public int TaskId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal? EstimatedHours { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public bool AllowSplitting { get; set; }
    public bool IsManuallyPinned { get; set; }
    public bool IsManualPriority { get; set; }
    public string Email { get; set; } = null!;
    public decimal? CourseCredits { get; set; }
    public double? DefaultTaskEstimatedHours { get; set; }
    public string? SharedStatus { get; set; }
}

// Task-event projection consumed by the dashboard aggregator.
public class DashboardTaskEventRow
{
    public int TaskId { get; set; }
    public int EventId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Status { get; set; }
}
