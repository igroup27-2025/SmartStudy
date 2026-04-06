using Microsoft.Data.SqlClient;
using System.Data;
using SmartStudy.Models;

namespace SmartStudy.DAL;

public class DBservices
{
    private readonly SqlHelper _sql;

    public DBservices(SqlHelper sql)
    {
        _sql = sql;
    }

    // =====================================================
    // USERS
    // =====================================================

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _sql.QuerySingleAsync("SS_Users_GetByEmail", MapUser,
            SqlHelper.Param("@Email", email));
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        var result = await _sql.ScalarAsync("SS_Users_ExistsByEmail",
            SqlHelper.Param("@Email", email));
        return Convert.ToInt32(result) == 1;
    }

    public async Task CreateUserAsync(string email, string firstName, string lastName, string password, string? authProvider = null)
    {
        await _sql.ExecuteAsync("SS_Users_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@FirstName", firstName),
            SqlHelper.Param("@LastName", lastName),
            SqlHelper.Param("@Password", password),
            SqlHelper.Param("@AuthProvider", authProvider));
    }

    public async Task UpdateUserProfileAsync(string email, string? firstName, string? lastName)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateProfile",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@FirstName", firstName),
            SqlHelper.Param("@LastName", lastName));
    }

    public async Task UpdateResetTokenAsync(string email, string token, DateTime expiry)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateResetToken",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@ResetToken", token),
            SqlHelper.Param("@ResetTokenExpiry", expiry));
    }

    public async Task ResetPasswordAsync(string email, string newPassword)
    {
        await _sql.ExecuteAsync("SS_Users_ResetPassword",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@NewPassword", newPassword));
    }

    public async Task SetOnboardingCompleteAsync(string email)
    {
        await _sql.ExecuteAsync("SS_Users_SetOnboardingComplete",
            SqlHelper.Param("@Email", email));
    }

    public async Task UpdateRuppinetFieldsAsync(string email, string? ruppinetId, string? ruppinetPassword, DateTime? lastSync = null)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateRuppinetFields",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@RuppinetId", ruppinetId),
            SqlHelper.Param("@RuppinetPassword", ruppinetPassword),
            SqlHelper.Param("@LastRuppinetSync", lastSync));
    }

    public async Task ClearRuppinetAsync(string email)
    {
        await _sql.ExecuteAsync("SS_Users_ClearRuppinet",
            SqlHelper.Param("@Email", email));
    }


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

    public async Task<NotificationSettings?> GetNotifSettingsByEmailAsync(string email)
    {
        return await _sql.QuerySingleAsync("SS_NotifSettings_GetByEmail", MapNotifSettings,
            SqlHelper.Param("@Email", email));
    }

    public async Task UpsertNotifSettingsAsync(string email, bool notifyBeforeTask, bool dailyMorningSummary,
        bool weeklyPlanReminder, bool enablePushNotification, TimeSpan? quietStart, TimeSpan? quietEnd)
    {
        await _sql.ExecuteAsync("SS_NotifSettings_Upsert",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@NotifyBeforeTask", notifyBeforeTask),
            SqlHelper.Param("@DailyMorningSummary", dailyMorningSummary),
            SqlHelper.Param("@WeeklyPlanReminder", weeklyPlanReminder),
            SqlHelper.Param("@EnablePushNotification", enablePushNotification),
            SqlHelper.Param("@QuietHoursStart", quietStart, SqlDbType.Time),
            SqlHelper.Param("@QuietHoursEnd", quietEnd, SqlDbType.Time));
    }

    public async Task CreateDefaultNotifSettingsAsync(string email)
    {
        await _sql.ExecuteAsync("SS_NotifSettings_CreateDefault",
            SqlHelper.Param("@Email", email));
    }

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

    public async Task<SchedulingPreferences?> GetSchedPrefsByEmailAsync(string email)
    {
        return await _sql.QuerySingleAsync("SS_SchedPrefs_GetByEmail", MapSchedPrefs,
            SqlHelper.Param("@Email", email));
    }

    public async Task UpsertSchedPrefsAsync(SchedulingPreferences p)
    {
        await _sql.ExecuteAsync("SS_SchedPrefs_Upsert",
            SqlHelper.Param("@Email", p.Email),
            SqlHelper.Param("@MaxDailyStudyHours", p.MaxDailyStudyHours),
            SqlHelper.Param("@MaxContinuousMinutes", p.MaxContinuousMinutes),
            SqlHelper.Param("@DayStartHour", p.DayStartHour),
            SqlHelper.Param("@DayEndHour", p.DayEndHour),
            SqlHelper.Param("@SleepHoursPerDay", p.SleepHoursPerDay),
            SqlHelper.Param("@LunchBreakStart", p.LunchBreakStart, SqlDbType.Time),
            SqlHelper.Param("@LunchBreakEnd", p.LunchBreakEnd, SqlDbType.Time),
            SqlHelper.Param("@BreakDurationMinutes", p.BreakDurationMinutes),
            SqlHelper.Param("@DefaultTaskEstimatedHours", p.DefaultTaskEstimatedHours),
            SqlHelper.Param("@MaxDailyTotalHours", p.MaxDailyTotalHours),
            SqlHelper.Param("@ExamPrepHoursPerDay", p.ExamPrepHoursPerDay),
            SqlHelper.Param("@ExamPrepDays", p.ExamPrepDays));
    }

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

    public async Task<List<Instructor>> GetAllInstructorsAsync()
    {
        return await _sql.QueryAsync("SS_Instructors_GetAll", r => new Instructor
        {
            InstructorId = r.GetInt32(r.GetOrdinal("InstructorId")),
            InstructorName = r.GetString(r.GetOrdinal("InstructorName"))
        });
    }

    // =====================================================
    // EVENTS (Full CRUD - Phase 5)
    // =====================================================

    public async Task<List<TypedEvent>> GetAllTypedEventsInRangeAsync(string email, DateTime? from, DateTime? to)
    {
        return await _sql.QueryAsync("SS_Events_GetAllTypedInRange", MapTypedEvent,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to));
    }

    public async Task<int> CreateClassEventAsync(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        var result = await _sql.ScalarAsync("SS_ClassEvents_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Location", location),
            SqlHelper.Param("@Duration", duration));
        return Convert.ToInt32(result);
    }

    public async Task<int> CreateTaskEventAsync(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        var result = await _sql.ScalarAsync("SS_TaskEvents_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@Priority", priority),
            SqlHelper.Param("@Status", status ?? "Scheduled"));
        return Convert.ToInt32(result);
    }

    public async Task<int> CreateWorkEventAsync(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime = null)
    {
        var result = await _sql.ScalarAsync("SS_WorkEvents_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@WorkPlace", workPlace),
            SqlHelper.Param("@TravelTime", travelTime));
        return Convert.ToInt32(result);
    }

    public async Task<int> CreatePersonalEventAsync(string email, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        var result = await _sql.ScalarAsync("SS_PersonalEvents_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@Description", description));
        return Convert.ToInt32(result);
    }

    public async Task UpdateClassEventAsync(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int courseId, string? location, decimal? duration)
    {
        await _sql.ExecuteAsync("SS_ClassEvents_Update",
            SqlHelper.Param("@EventId", eventId),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Location", location),
            SqlHelper.Param("@Duration", duration));
    }

    public async Task UpdateTaskEventAsync(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, int taskId, string? priority, string? status)
    {
        await _sql.ExecuteAsync("SS_TaskEvents_Update",
            SqlHelper.Param("@EventId", eventId),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@Priority", priority),
            SqlHelper.Param("@Status", status));
    }

    public async Task UpdateWorkEventAsync(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? workPlace, int? travelTime)
    {
        await _sql.ExecuteAsync("SS_WorkEvents_Update",
            SqlHelper.Param("@EventId", eventId),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@WorkPlace", workPlace),
            SqlHelper.Param("@TravelTime", travelTime));
    }

    public async Task UpdatePersonalEventAsync(int eventId, DateTime from, DateTime to, bool recurring, DateTime? recurrenceEndDate, string? type, string? description)
    {
        await _sql.ExecuteAsync("SS_PersonalEvents_Update",
            SqlHelper.Param("@EventId", eventId),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Recurring", recurring),
            SqlHelper.Param("@RecurrenceEndDate", recurrenceEndDate),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@Description", description));
    }

    public async Task DeleteEventAsync(int eventId)
    {
        await _sql.ExecuteAsync("SS_Events_Delete",
            SqlHelper.Param("@EventId", eventId));
    }

    public async Task<string?> GetEventOwnerEmailAsync(int eventId)
    {
        var result = await _sql.ScalarAsync("SS_Events_GetOwnerEmail",
            SqlHelper.Param("@EventId", eventId));
        return result as string;
    }

    public async Task<string> GetEventSubtypeAsync(int eventId)
    {
        var result = await _sql.ScalarAsync("SS_Events_GetSubtype",
            SqlHelper.Param("@EventId", eventId));
        return result?.ToString() ?? "unknown";
    }

    public async Task ChangeEventTypeAsync(int eventId, string oldType, string newType, string? workPlace, int? travelTime, string? personalType, string? description)
    {
        await _sql.ExecuteAsync("SS_Events_ChangeType",
            SqlHelper.Param("@EventId", eventId),
            SqlHelper.Param("@OldType", oldType),
            SqlHelper.Param("@NewType", newType),
            SqlHelper.Param("@WorkPlace", workPlace),
            SqlHelper.Param("@TravelTime", travelTime),
            SqlHelper.Param("@Type", personalType),
            SqlHelper.Param("@Description", description));
    }

    public async Task<int> CountConflictingTaskEventsAsync(string email, DateTime from, DateTime to, int excludeEventId)
    {
        var result = await _sql.ScalarAsync("SS_Events_CountConflictingTaskEvents",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@ExcludeEventId", excludeEventId));
        return Convert.ToInt32(result);
    }

    public async Task<List<TypedEvent>> GetConflictingEventsAsync(string email, DateTime from, DateTime to, int? excludeEventId)
    {
        return await _sql.QueryAsync("SS_Events_CheckConflicts", MapTypedEvent,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@ExcludeEventId", excludeEventId));
    }

    public async Task PinTaskAsync(int taskId)
    {
        await _sql.ExecuteAsync("SS_Tasks_PinTask",
            SqlHelper.Param("@TaskId", taskId));
    }

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

    public async Task<List<CourseWithEnrollment>> GetCoursesByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_Courses_GetByUser", r => new CourseWithEnrollment
        {
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            WeeklyHours = r.IsDBNull(r.GetOrdinal("WeeklyHours")) ? null : r.GetDecimal(r.GetOrdinal("WeeklyHours")),
            Credits = r.IsDBNull(r.GetOrdinal("Credits")) ? null : r.GetDecimal(r.GetOrdinal("Credits")),
            Semester = r.IsDBNull(r.GetOrdinal("Semester")) ? null : r.GetString(r.GetOrdinal("Semester")),
            InstructorId = r.IsDBNull(r.GetOrdinal("InstructorId")) ? null : r.GetInt32(r.GetOrdinal("InstructorId")),
            InstructorName = r.IsDBNull(r.GetOrdinal("InstructorName")) ? null : r.GetString(r.GetOrdinal("InstructorName")),
            DefaultTaskEstimatedHours = r.IsDBNull(r.GetOrdinal("DefaultTaskEstimatedHours")) ? null : r.GetDouble(r.GetOrdinal("DefaultTaskEstimatedHours")),
            ExamPrepHoursPerDay = r.IsDBNull(r.GetOrdinal("ExamPrepHoursPerDay")) ? null : r.GetDouble(r.GetOrdinal("ExamPrepHoursPerDay")),
            ExamPrepDays = r.IsDBNull(r.GetOrdinal("ExamPrepDays")) ? null : r.GetInt32(r.GetOrdinal("ExamPrepDays")),
            StudyPartnerEmail = r.IsDBNull(r.GetOrdinal("StudyPartnerEmail")) ? null : r.GetString(r.GetOrdinal("StudyPartnerEmail")),
            SharedByDefault = Convert.ToBoolean(r.GetValue(r.GetOrdinal("SharedByDefault"))),
            CourseShareApproved = Convert.ToBoolean(r.GetValue(r.GetOrdinal("CourseShareApproved"))),
            TaskCount = r.GetInt32(r.GetOrdinal("TaskCount")),
            ExamCount = r.GetInt32(r.GetOrdinal("ExamCount"))
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<Course?> GetCourseByIdAsync(int courseId)
    {
        return await _sql.QuerySingleAsync("SS_Courses_GetById", MapCourse,
            SqlHelper.Param("@CourseId", courseId));
    }

    public async Task<int> GetMaxCourseIdAsync()
    {
        var result = await _sql.ScalarAsync("SS_Courses_GetMaxId");
        return Convert.ToInt32(result ?? 0);
    }

    public async Task CreateCourseAsync(int courseId, string courseName, decimal? weeklyHours, decimal? credits, string? semester, int? instructorId)
    {
        await _sql.ExecuteAsync("SS_Courses_Create",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@CourseName", courseName),
            SqlHelper.Param("@WeeklyHours", weeklyHours),
            SqlHelper.Param("@Credits", credits),
            SqlHelper.Param("@Semester", semester),
            SqlHelper.Param("@InstructorId", instructorId));
    }

    public async Task UpdateCourseAsync(int courseId, string? courseName = null, decimal? weeklyHours = null, decimal? credits = null,
        string? semester = null, int? instructorId = null, double? defaultTaskEstimatedHours = null, double? examPrepHoursPerDay = null, int? examPrepDays = null)
    {
        await _sql.ExecuteAsync("SS_Courses_Update",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@CourseName", courseName),
            SqlHelper.Param("@WeeklyHours", weeklyHours),
            SqlHelper.Param("@Credits", credits),
            SqlHelper.Param("@Semester", semester),
            SqlHelper.Param("@InstructorId", instructorId),
            SqlHelper.Param("@DefaultTaskEstimatedHours", defaultTaskEstimatedHours),
            SqlHelper.Param("@ExamPrepHoursPerDay", examPrepHoursPerDay),
            SqlHelper.Param("@ExamPrepDays", examPrepDays));
    }

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

    public async Task<bool> UserCourseExistsAsync(string email, int courseId)
    {
        var result = await _sql.ScalarAsync("SS_UserCourses_Exists",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId));
        return Convert.ToInt32(result) == 1;
    }

    public async Task CreateUserCourseAsync(string email, int courseId)
    {
        await _sql.ExecuteAsync("SS_UserCourses_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId));
    }

    public async Task DeleteUserCourseAsync(string email, int courseId)
    {
        await _sql.ExecuteAsync("SS_UserCourses_Delete",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId));
    }

    public async Task UpdateStudyPartnerAsync(string email, int courseId, string? partnerEmail)
    {
        await _sql.ExecuteAsync("SS_UserCourses_UpdatePartner",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@StudyPartnerEmail", partnerEmail));
    }

    public async Task UpdateSharedByDefaultAsync(string email, int courseId, bool sharedByDefault)
    {
        await _sql.ExecuteAsync("SS_UserCourses_UpdateSharedByDefault",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@SharedByDefault", sharedByDefault));
    }

    public async Task<List<int>> GetCourseIdsByEmailAsync(string email)
    {
        return await _sql.QueryAsync("SS_UserCourses_GetCourseIdsByEmail",
            r => r.GetInt32(0),
            SqlHelper.Param("@Email", email));
    }

    // =====================================================
    // EXAMS
    // =====================================================

    public async Task<List<ExamWithCourse>> GetExamsByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_Exams_GetByUser", MapExamWithCourse,
            SqlHelper.Param("@Email", email));
    }

    public async Task<ExamWithCourse?> GetExamByIdAsync(int examId, string email)
    {
        return await _sql.QuerySingleAsync("SS_Exams_GetById", MapExamWithCourse,
            SqlHelper.Param("@ExamId", examId),
            SqlHelper.Param("@Email", email));
    }

    public async Task<int> CreateExamAsync(int courseId, DateTime date, TimeSpan time, string session, int? duration, bool isTakingExam)
    {
        var result = await _sql.ScalarAsync("SS_Exams_Create",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Date", date),
            SqlHelper.Param("@Time", time, SqlDbType.Time),
            SqlHelper.Param("@Session", session),
            SqlHelper.Param("@Duration", duration),
            SqlHelper.Param("@IsTakingExam", isTakingExam));
        return Convert.ToInt32(result);
    }

    public async Task UpdateExamAsync(int examId, int? courseId = null, DateTime? date = null, TimeSpan? time = null, string? session = null, int? duration = null)
    {
        await _sql.ExecuteAsync("SS_Exams_Update",
            SqlHelper.Param("@ExamId", examId),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Date", date),
            SqlHelper.Param("@Time", time, SqlDbType.Time),
            SqlHelper.Param("@Session", session),
            SqlHelper.Param("@Duration", duration));
    }

    public async Task ToggleExamTakingAsync(int examId)
    {
        await _sql.ExecuteAsync("SS_Exams_ToggleTaking",
            SqlHelper.Param("@ExamId", examId));
    }

    public async Task DeleteExamAsync(int examId)
    {
        await _sql.ExecuteAsync("SS_Exams_Delete",
            SqlHelper.Param("@ExamId", examId));
    }

    public async Task DeleteStudyTasksForExamAsync(string email, int courseId, DateTime examDate)
    {
        await _sql.ExecuteAsync("SS_Tasks_DeleteStudyTasksForExam",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@ExamDate", examDate));
    }

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
            IsTakingExam = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsTakingExam")))
        };
    }

    // =====================================================
    // TASKS
    // =====================================================

    public async Task<List<TaskWithCourse>> GetTasksByUserAsync(string email, int? courseId = null, bool? completed = null)
    {
        return await _sql.QueryAsync("SS_Tasks_GetByUser", MapTaskWithCourse,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Completed", completed));
    }

    public async Task<List<TaskWithCourse>> GetSubTasksAsync(int parentTaskId)
    {
        return await _sql.QueryAsync("SS_Tasks_GetSubTasks", MapTaskWithCourse,
            SqlHelper.Param("@ParentTaskId", parentTaskId));
    }

    public async Task<TaskWithCourse?> GetTaskByIdAsync(int taskId)
    {
        return await _sql.QuerySingleAsync("SS_Tasks_GetById", MapTaskWithCourse,
            SqlHelper.Param("@TaskId", taskId));
    }

    public async Task<int> CreateTaskAsync(int courseId, string email, string title, string type,
        decimal? estimatedHours, DateTime? dueDate, int? parentTaskId, bool allowSplitting,
        string? priority, bool isManualPriority, bool isManuallyPinned = false)
    {
        var result = await _sql.ScalarAsync("SS_Tasks_Create",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@Title", title),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@EstimatedHours", estimatedHours),
            SqlHelper.Param("@DueDate", dueDate),
            SqlHelper.Param("@ParentTaskId", parentTaskId),
            SqlHelper.Param("@AllowSplitting", allowSplitting),
            SqlHelper.Param("@Priority", priority),
            SqlHelper.Param("@IsManualPriority", isManualPriority),
            SqlHelper.Param("@IsManuallyPinned", isManuallyPinned));
        return Convert.ToInt32(result);
    }

    public async Task UpdateTaskAsync(int taskId, int? courseId = null, string? title = null, string? type = null,
        decimal? estimatedHours = null, DateTime? dueDate = null, bool? isCompleted = null,
        bool? allowSplitting = null, bool? isManuallyPinned = null, string? priority = null,
        bool? isManualPriority = null, decimal? actualHours = null)
    {
        await _sql.ExecuteAsync("SS_Tasks_Update",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Title", title),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@EstimatedHours", estimatedHours),
            SqlHelper.Param("@DueDate", dueDate),
            SqlHelper.Param("@IsCompleted", isCompleted),
            SqlHelper.Param("@AllowSplitting", allowSplitting),
            SqlHelper.Param("@IsManuallyPinned", isManuallyPinned),
            SqlHelper.Param("@Priority", priority),
            SqlHelper.Param("@IsManualPriority", isManualPriority),
            SqlHelper.Param("@ActualHours", actualHours));
    }

    public async Task DeleteTaskAsync(int taskId)
    {
        await _sql.ExecuteAsync("SS_Tasks_Delete",
            SqlHelper.Param("@TaskId", taskId));
    }

    public async Task CompleteTaskAsync(int taskId, bool isCompleted, decimal? actualHours = null)
    {
        await _sql.ExecuteAsync("SS_Tasks_Complete",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@IsCompleted", isCompleted),
            SqlHelper.Param("@ActualHours", actualHours));
    }

    public async Task<List<TaskEventInfo>> GetTaskEventsAsync(int taskId)
    {
        return await _sql.QueryAsync("SS_Tasks_GetTaskEvents", r => new TaskEventInfo
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            ActualHours = r.IsDBNull(r.GetOrdinal("ActualHours")) ? null : r.GetDecimal(r.GetOrdinal("ActualHours")),
            Status = r.IsDBNull(r.GetOrdinal("Status")) ? null : r.GetString(r.GetOrdinal("Status"))
        }, SqlHelper.Param("@TaskId", taskId));
    }

    public async Task<SharedTaskInfo?> GetSharedInfoAsync(int taskId)
    {
        var rows = await _sql.QueryAsync("SS_Tasks_GetSharedInfo", r => new
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            SharedStatus = r.GetString(r.GetOrdinal("SharedStatus")),
            CreatedByEmail = r.GetString(r.GetOrdinal("CreatedByEmail")),
            MemberEmail = r.IsDBNull(r.GetOrdinal("MemberEmail")) ? null : r.GetString(r.GetOrdinal("MemberEmail")),
            ResponseStatus = r.IsDBNull(r.GetOrdinal("ResponseStatus")) ? null : r.GetString(r.GetOrdinal("ResponseStatus")),
            FirstName = r.IsDBNull(r.GetOrdinal("FirstName")) ? null : r.GetString(r.GetOrdinal("FirstName")),
            LastName = r.IsDBNull(r.GetOrdinal("LastName")) ? null : r.GetString(r.GetOrdinal("LastName"))
        }, SqlHelper.Param("@TaskId", taskId));

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

    public async Task<bool> CheckAllSiblingsCompleteAsync(int parentTaskId)
    {
        var result = await _sql.ScalarAsync("SS_Tasks_CheckAllSiblingsComplete",
            SqlHelper.Param("@ParentTaskId", parentTaskId));
        return Convert.ToInt32(result) == 1;
    }

    public async Task<List<MLDataRow>> GetMLDataAsync(string email, int courseId)
    {
        return await _sql.QueryAsync("SS_Tasks_GetMLData", r => new MLDataRow
        {
            ActualHours = r.GetDecimal(r.GetOrdinal("ActualHours")),
            EstimatedHours = r.GetDecimal(r.GetOrdinal("EstimatedHours"))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@CourseId", courseId));
    }

    public async Task<List<MLInsightRow>> GetMLInsightsAsync(string email)
    {
        return await _sql.QueryAsync("SS_Tasks_GetMLInsights", r => new MLInsightRow
        {
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            TaskCount = r.GetInt32(r.GetOrdinal("TaskCount")),
            AvgEstimated = r.GetDouble(r.GetOrdinal("AvgEstimated")),
            AvgActual = r.GetDouble(r.GetOrdinal("AvgActual")),
            Accuracy = r.GetDouble(r.GetOrdinal("Accuracy"))
        }, SqlHelper.Param("@Email", email));
    }

    // Shared Tasks create
    public async Task CreateSharedTaskAsync(int taskId, string createdByEmail, string sharedStatus = "Pending")
    {
        await _sql.ExecuteAsync("SS_SharedTasks_Create",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@CreatedByEmail", createdByEmail),
            SqlHelper.Param("@SharedStatus", sharedStatus));
    }

    public async Task UpdateSharedTaskStatusAsync(int taskId, string status)
    {
        await _sql.ExecuteAsync("SS_SharedTasks_UpdateStatus",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@SharedStatus", status));
    }

    public async Task CreateSharedTaskMemberAsync(int taskId, string email, string responseStatus = "Pending", DateTime? respondedAt = null)
    {
        await _sql.ExecuteAsync("SS_SharedTaskMembers_Create",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@ResponseStatus", responseStatus),
            SqlHelper.Param("@RespondedAt", respondedAt));
    }

    public async Task<UserCourse?> GetUserCourseAsync(string email, int courseId)
    {
        return await _sql.QuerySingleAsync("SS_UserCourses_GetByEmailAndCourse", r => new UserCourse
        {
            Email = r.GetString(r.GetOrdinal("Email")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            StudyPartnerEmail = r.IsDBNull(r.GetOrdinal("StudyPartnerEmail")) ? null : r.GetString(r.GetOrdinal("StudyPartnerEmail")),
            SharedByDefault = Convert.ToBoolean(r.GetValue(r.GetOrdinal("SharedByDefault"))),
            CourseShareApproved = Convert.ToBoolean(r.GetValue(r.GetOrdinal("CourseShareApproved")))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@CourseId", courseId));
    }

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

    public async Task<List<FriendRequestRow>> GetFriendRequestsByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_FriendRequests_GetByUser", r => new FriendRequestRow
        {
            RequestId = r.GetInt32(r.GetOrdinal("RequestId")),
            RequesterEmail = r.GetString(r.GetOrdinal("RequesterEmail")),
            AddresseeEmail = r.GetString(r.GetOrdinal("AddresseeEmail")),
            Status = r.GetString(r.GetOrdinal("Status")),
            RequestedAt = r.GetDateTime(r.GetOrdinal("RequestedAt")),
            RespondedAt = r.IsDBNull(r.GetOrdinal("RespondedAt")) ? null : r.GetDateTime(r.GetOrdinal("RespondedAt")),
            FriendEmail = r.GetString(r.GetOrdinal("FriendEmail")),
            FirstName = r.GetString(r.GetOrdinal("FirstName")),
            LastName = r.GetString(r.GetOrdinal("LastName"))
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<int> CreateFriendRequestAsync(string requesterEmail, string addresseeEmail)
    {
        var result = await _sql.ScalarAsync("SS_FriendRequests_Create",
            SqlHelper.Param("@RequesterEmail", requesterEmail),
            SqlHelper.Param("@AddresseeEmail", addresseeEmail));
        return Convert.ToInt32(result);
    }


    public async Task<FriendRequestBasic?> UpdateFriendRequestStatusAsync(int requestId, string addresseeEmail, string newStatus)
    {
        return await _sql.QuerySingleAsync("SS_FriendRequests_UpdateStatus", r => new FriendRequestBasic
        {
            RequestId = r.GetInt32(r.GetOrdinal("RequestId")),
            RequesterEmail = r.GetString(r.GetOrdinal("RequesterEmail")),
            AddresseeEmail = r.GetString(r.GetOrdinal("AddresseeEmail")),
            Status = r.GetString(r.GetOrdinal("Status")),
            RequestedAt = r.GetDateTime(r.GetOrdinal("RequestedAt")),
            RespondedAt = r.IsDBNull(r.GetOrdinal("RespondedAt")) ? null : r.GetDateTime(r.GetOrdinal("RespondedAt"))
        },
            SqlHelper.Param("@RequestId", requestId),
            SqlHelper.Param("@AddresseeEmail", addresseeEmail),
            SqlHelper.Param("@NewStatus", newStatus));
    }

    // =====================================================
    // FRIENDSHIPS
    // =====================================================

    public async Task<bool> FriendshipExistsAsync(string email1, string email2)
    {
        var (e1, e2) = NormalizePair(email1, email2);
        var result = await _sql.ScalarAsync("SS_Friendships_ExistsPair",
            SqlHelper.Param("@Email1", e1),
            SqlHelper.Param("@Email2", e2));
        return Convert.ToInt32(result) == 1;
    }

    public async Task<List<FriendshipRow>> GetFriendshipsByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_Friendships_GetByUser", r => new FriendshipRow
        {
            FriendshipId = r.GetInt32(r.GetOrdinal("FriendshipId")),
            Email1 = r.GetString(r.GetOrdinal("Email1")),
            Email2 = r.GetString(r.GetOrdinal("Email2")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
            IsActive = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsActive"))),
            FriendEmail = r.GetString(r.GetOrdinal("FriendEmail")),
            FirstName = r.GetString(r.GetOrdinal("FirstName")),
            LastName = r.GetString(r.GetOrdinal("LastName"))
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<int> CreateFriendshipAsync(string email1, string email2)
    {
        var result = await _sql.ScalarAsync("SS_Friendships_Create",
            SqlHelper.Param("@Email1", email1),
            SqlHelper.Param("@Email2", email2));
        return Convert.ToInt32(result);
    }

    public async Task<bool> DeactivateFriendshipAsync(int friendshipId, string email)
    {
        var result = await _sql.ScalarAsync("SS_Friendships_Deactivate",
            SqlHelper.Param("@FriendshipId", friendshipId),
            SqlHelper.Param("@Email", email));
        return Convert.ToInt32(result) > 0;
    }


    public async Task<FriendshipBasic?> GetFriendshipForUserAsync(int friendshipId, string email)
    {
        return await _sql.QuerySingleAsync("SS_Collaboration_GetFriendshipForUser", r => new FriendshipBasic
        {
            FriendshipId = r.GetInt32(r.GetOrdinal("FriendshipId")),
            Email1 = r.GetString(r.GetOrdinal("Email1")),
            Email2 = r.GetString(r.GetOrdinal("Email2")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt")),
            IsActive = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsActive")))
        }, SqlHelper.Param("@FriendshipId", friendshipId), SqlHelper.Param("@Email", email));
    }

    public static (string, string) NormalizePair(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? (a, b) : (b, a);

    // =====================================================
    // SHARED TASKS (full CRUD)
    // =====================================================

    public async Task<List<SharedTaskFullRow>> GetSharedTasksByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_SharedTasks_GetByUser", MapSharedTaskFullRow,
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<SharedTaskFullRow>> GetSharedTaskByTaskIdAsync(int taskId)
    {
        return await _sql.QueryAsync("SS_SharedTasks_GetByTaskId", MapSharedTaskFullRow,
            SqlHelper.Param("@TaskId", taskId));
    }

    public async Task<bool> SharedTaskExistsAsync(int taskId)
    {
        var result = await _sql.ScalarAsync("SS_SharedTasks_ExistsByTaskId",
            SqlHelper.Param("@TaskId", taskId));
        return Convert.ToInt32(result) == 1;
    }


    public async Task<bool> UpdateSharedTaskMemberStatusAsync(int taskId, string email, string responseStatus)
    {
        var result = await _sql.ScalarAsync("SS_SharedTaskMembers_UpdateStatus",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@ResponseStatus", responseStatus));
        return Convert.ToInt32(result) > 0;
    }

    public async Task<bool> AllSharedTaskMembersAcceptedAsync(int taskId)
    {
        var result = await _sql.ScalarAsync("SS_SharedTaskMembers_AllAccepted",
            SqlHelper.Param("@TaskId", taskId));
        return Convert.ToInt32(result) == 1;
    }

    public async Task<List<string>> GetSharedTaskMemberEmailsAsync(int taskId)
    {
        return await _sql.QueryAsync("SS_SharedTaskMembers_GetEmails",
            r => r.GetString(0),
            SqlHelper.Param("@TaskId", taskId));
    }

    public async Task UpdateSharedTaskMemberCopyTaskIdAsync(int taskId, string email, int copyTaskId)
    {
        await _sql.ExecuteAsync("SS_SharedTaskMembers_UpdateCopyTaskId",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CopyTaskId", copyTaskId));
    }

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

    public async Task<bool> SetCourseShareApprovedAsync(string email, int courseId)
    {
        var result = await _sql.ScalarAsync("SS_UserCourses_SetCourseShareApproved",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId));
        return Convert.ToInt32(result) > 0;
    }

    public async Task<List<PendingMemberForCourseRow>> GetPendingMembersForCourseAsync(string email, int courseId)
    {
        return await _sql.QueryAsync("SS_Collaboration_GetPendingMembersForCourse", r => new PendingMemberForCourseRow
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            ResponseStatus = r.GetString(r.GetOrdinal("ResponseStatus")),
            RespondedAt = r.IsDBNull(r.GetOrdinal("RespondedAt")) ? null : r.GetDateTime(r.GetOrdinal("RespondedAt")),
            CreatedByEmail = r.GetString(r.GetOrdinal("CreatedByEmail")),
            SharedStatus = r.GetString(r.GetOrdinal("SharedStatus"))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@CourseId", courseId));
    }

    // =====================================================
    // NOTIFICATIONS
    // =====================================================

    public async Task<int> CreateNotificationAsync(string email, string type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null)
    {
        var result = await _sql.ScalarAsync("SS_Notifications_Create",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@Title", title),
            SqlHelper.Param("@Message", message),
            SqlHelper.Param("@RelatedEntityId", relatedEntityId),
            SqlHelper.Param("@RelatedEntityType", relatedEntityType));
        return Convert.ToInt32(result);
    }

    public async Task<(List<NotificationRow> Notifications, int UnreadCount)> GetNotificationsByUserAsync(string email)
    {
        var notifications = new List<NotificationRow>();
        int unreadCount = 0;

        await _sql.QueryMultipleAsync("SS_Notifications_GetByUser", reader =>
        {
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
        },
        SqlHelper.Param("@Email", email));

        return (notifications, unreadCount);
    }

    public async Task<int> GetUnreadNotificationCountAsync(string email)
    {
        var result = await _sql.ScalarAsync("SS_Notifications_GetUnreadCount",
            SqlHelper.Param("@Email", email));
        return Convert.ToInt32(result);
    }

    public async Task MarkNotificationsReadAsync(string email, List<int> notificationIds)
    {
        var ids = string.Join(",", notificationIds);
        await _sql.ExecuteAsync("SS_Notifications_MarkRead",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@NotificationIds", ids));
    }

    public async Task MarkAllNotificationsReadAsync(string email)
    {
        await _sql.ExecuteAsync("SS_Notifications_MarkAllRead",
            SqlHelper.Param("@Email", email));
    }

    public async Task<bool> IsNotificationDuplicateAsync(string email, string type, int? relatedEntityId = null, string? relatedEntityType = null, int sinceHours = 24)
    {
        var result = await _sql.ScalarAsync("SS_Notifications_IsDuplicate",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@RelatedEntityId", relatedEntityId),
            SqlHelper.Param("@RelatedEntityType", relatedEntityType),
            SqlHelper.Param("@SinceHours", sinceHours));
        return Convert.ToInt32(result) == 1;
    }

    public async Task<List<UpcomingDeadlineTask>> GetUpcomingDeadlineTasksAsync(string email)
    {
        return await _sql.QueryAsync("SS_Notifications_GetUpcomingDeadlineTasks", r => new UpcomingDeadlineTask
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            Title = r.GetString(r.GetOrdinal("Title")),
            CourseName = r.GetString(r.GetOrdinal("CourseName"))
        },
        SqlHelper.Param("@Email", email));
    }

    public async Task<List<DailySummaryTask>> GetDailySummaryDataAsync(string email)
    {
        return await _sql.QueryAsync("SS_Notifications_GetDailySummaryData", r => new DailySummaryTask
        {
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            Title = r.GetString(r.GetOrdinal("Title")),
            CourseName = r.GetString(r.GetOrdinal("CourseName"))
        },
        SqlHelper.Param("@Email", email));
    }

    public async Task<(int TaskCount, int ExamCount)> GetWeeklyPlanDataAsync(string email)
    {
        int taskCount = 0;
        int examCount = 0;

        await _sql.QueryMultipleAsync("SS_Notifications_GetWeeklyPlanData", reader =>
        {
            if (reader.Read())
                taskCount = reader.GetInt32(0);
            if (reader.NextResult() && reader.Read())
                examCount = reader.GetInt32(0);
        },
        SqlHelper.Param("@Email", email));

        return (taskCount, examCount);
    }

    public async Task<bool> HasRecentWeeklyReminderAsync(string email)
    {
        var result = await _sql.ScalarAsync("SS_Notifications_HasRecentWeekly",
            SqlHelper.Param("@Email", email));
        return Convert.ToInt32(result) == 1;
    }

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
    // SCHEDULING / STRESS / SERVICES SUPPORT
    // =====================================================

    public async Task<List<Event>> GetBaseEventsInRangeOrRecurringAsync(string email, DateTime rangeStart, DateTime rangeEnd)
    {
        return await _sql.QueryAsync("SS_Events_GetBaseInRangeOrRecurring", r => new Event
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Recurring = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Recurring"))),
            RecurrenceEndDate = r.IsDBNull(r.GetOrdinal("RecurrenceEndDate")) ? null : r.GetDateTime(r.GetOrdinal("RecurrenceEndDate"))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@RangeStart", rangeStart), SqlHelper.Param("@RangeEnd", rangeEnd));
    }

    public async Task<List<Event>> GetEventsInDateRangeAsync(string email, DateTime from, DateTime to)
    {
        return await _sql.QueryAsync("SS_Events_GetInDateRange", r => new Event
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Recurring = Convert.ToBoolean(r.GetValue(r.GetOrdinal("Recurring"))),
            RecurrenceEndDate = r.IsDBNull(r.GetOrdinal("RecurrenceEndDate")) ? null : r.GetDateTime(r.GetOrdinal("RecurrenceEndDate"))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@From", from), SqlHelper.Param("@To", to));
    }

    public async Task<List<SchedulingTaskRow>> GetIncompleteLeafTasksAsync(string email)
    {
        return await _sql.QueryAsync("SS_Tasks_GetIncompleteLeaf", MapSchedulingTaskRow,
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<SchedulingTaskRow>> GetAllIncompleteTasksAsync(string email)
    {
        return await _sql.QueryAsync("SS_Tasks_GetAllIncomplete", r =>
        {
            var row = MapSchedulingTaskRow(r);
            row.SubTaskCount = r.GetInt32(r.GetOrdinal("SubTaskCount"));
            row.TaskEventCount = r.GetInt32(r.GetOrdinal("TaskEventCount"));
            row.HasNeedReview = Convert.ToBoolean(r.GetValue(r.GetOrdinal("HasNeedReview")));
            return row;
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<List<int>> GetPinnedTaskIdsAsync(string email)
    {
        return await _sql.QueryAsync("SS_Tasks_GetPinnedIds", r => r.GetInt32(0),
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<int>> GetNeedReviewTaskIdsAsync(string email)
    {
        return await _sql.QueryAsync("SS_TaskEvents_GetNeedReviewTaskIds", r => r.GetInt32(0),
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<TaskEventRow>> GetTaskEventsByUserAndStatusAsync(string email, string status1, string? status2 = null)
    {
        return await _sql.QueryAsync("SS_TaskEvents_GetByUserAndStatus", MapTaskEventRow,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@Status1", status1),
            SqlHelper.Param("@Status2", status2));
    }


    public async Task<List<TaskEventRow>> GetTaskEventsByTaskIdsAndStatusesAsync(int taskId1, int taskId2)
    {
        return await _sql.QueryAsync("SS_TaskEvents_GetByTaskIdsAndStatuses", MapTaskEventRow,
            SqlHelper.Param("@TaskId1", taskId1),
            SqlHelper.Param("@TaskId2", taskId2));
    }

    public async Task<List<TaskEventRow>> GetTaskEventsInRangeAsync(string email, DateTime from, DateTime to, string? status = null)
    {
        return await _sql.QueryAsync("SS_TaskEvents_GetByUserInRange", MapTaskEventRow,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to),
            SqlHelper.Param("@Status", status));
    }

    public async Task DeleteEventByIdAsync(int eventId)
    {
        await _sql.ExecuteAsync("SS_Events_DeleteById",
            SqlHelper.Param("@EventId", eventId));
    }

    public async Task<List<int>> GetClassEventIdsByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_ClassEvents_GetIdsByUser", r => r.GetInt32(0),
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<WorkEventRow>> GetWorkEventsByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_WorkEvents_GetByUser", r => new WorkEventRow
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            WorkPlace = r.IsDBNull(r.GetOrdinal("WorkPlace")) ? null : r.GetString(r.GetOrdinal("WorkPlace")),
            TravelTime = r.IsDBNull(r.GetOrdinal("TravelTime")) ? null : r.GetInt32(r.GetOrdinal("TravelTime"))
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<List<PersonalEventRow>> GetPersonalEventsByUserAsync(string email)
    {
        return await _sql.QueryAsync("SS_PersonalEvents_GetByUser", r => new PersonalEventRow
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Type = r.IsDBNull(r.GetOrdinal("Type")) ? null : r.GetString(r.GetOrdinal("Type")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description"))
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<List<SchedulingExamRow>> GetExamsForSchedulingAsync(string email, DateTime rangeStart, DateTime rangeEnd)
    {
        return await _sql.QueryAsync("SS_Exams_GetForScheduling", r => new SchedulingExamRow
        {
            ExamId = r.GetInt32(r.GetOrdinal("ExamId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            Date = r.GetDateTime(r.GetOrdinal("Date")),
            Time = r.GetTimeSpan(r.GetOrdinal("Time")),
            Session = r.GetString(r.GetOrdinal("Session")),
            Duration = r.IsDBNull(r.GetOrdinal("Duration")) ? null : r.GetInt32(r.GetOrdinal("Duration")),
            IsTakingExam = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsTakingExam"))),
            CourseExamPrepHoursPerDay = r.IsDBNull(r.GetOrdinal("CourseExamPrepHoursPerDay")) ? null : r.GetDouble(r.GetOrdinal("CourseExamPrepHoursPerDay")),
            CourseExamPrepDays = r.IsDBNull(r.GetOrdinal("CourseExamPrepDays")) ? null : r.GetInt32(r.GetOrdinal("CourseExamPrepDays"))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@RangeStart", rangeStart), SqlHelper.Param("@RangeEnd", rangeEnd));
    }

    public async Task<List<SchedulingExamRow>> GetUpcomingExamsAsync(string email, DateTime fromDate)
    {
        return await _sql.QueryAsync("SS_Exams_GetUpcoming", r => new SchedulingExamRow
        {
            ExamId = r.GetInt32(r.GetOrdinal("ExamId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            CourseName = r.GetString(r.GetOrdinal("CourseName")),
            Date = r.GetDateTime(r.GetOrdinal("Date")),
            Time = r.GetTimeSpan(r.GetOrdinal("Time")),
            Session = r.GetString(r.GetOrdinal("Session")),
            Duration = r.IsDBNull(r.GetOrdinal("Duration")) ? null : r.GetInt32(r.GetOrdinal("Duration")),
            IsTakingExam = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsTakingExam")))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@FromDate", fromDate));
    }

    public async Task<List<MLCompletedTaskRow>> GetCompletedTasksForMLAsync(string email)
    {
        return await _sql.QueryAsync("SS_Tasks_GetCompletedForML", r => new MLCompletedTaskRow
        {
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            ActualHours = r.GetDouble(r.GetOrdinal("ActualHours")),
            EstimatedHours = r.GetDouble(r.GetOrdinal("EstimatedHours"))
        }, SqlHelper.Param("@Email", email));
    }

    public async Task<SimpleTaskRow?> GetStudyForExamTaskAsync(string email, int courseId, DateTime dueDate)
    {
        return await _sql.QuerySingleAsync("SS_Tasks_GetStudyForExam", MapSimpleTaskRow,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@DueDate", dueDate));
    }

    public async Task<List<int>> GetOrphanedStudyTaskIdsAsync(string email)
    {
        return await _sql.QueryAsync("SS_Tasks_GetOrphanedStudyTasks", r => r.GetInt32(0),
            SqlHelper.Param("@Email", email));
    }


    public async Task DeleteTaskWithEventsAsync(int taskId)
    {
        await _sql.ExecuteAsync("SS_Tasks_DeleteWithEvents",
            SqlHelper.Param("@TaskId", taskId));
    }

    public async Task UpdateTaskPriorityAsync(int taskId, string priority)
    {
        await _sql.ExecuteAsync("SS_Tasks_UpdatePriority",
            SqlHelper.Param("@TaskId", taskId),
            SqlHelper.Param("@Priority", priority));
    }

    public async Task<SimpleTaskRow?> FindTaskByMatchAsync(string email, string title, int courseId, DateTime? dueDate)
    {
        return await _sql.QuerySingleAsync("SS_Tasks_FindByMatch", MapSimpleTaskRow,
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@Title", title),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@DueDate", dueDate));
    }

    public async Task<PersonalEventRow?> FindPersonalEventByGcalIdAsync(string email, string gcalMarker)
    {
        return await _sql.QuerySingleAsync("SS_PersonalEvents_FindByGcalId", r => new PersonalEventRow
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            Type = r.IsDBNull(r.GetOrdinal("Type")) ? null : r.GetString(r.GetOrdinal("Type")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description"))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@GcalMarker", gcalMarker));
    }

    public async Task UpdateEventTimesAsync(int eventId, DateTime from, DateTime to)
    {
        await _sql.ExecuteAsync("SS_Events_UpdateTimes",
            SqlHelper.Param("@EventId", eventId),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to));
    }

    public async Task<int> CountGcalEventsAsync(string email)
    {
        var result = await _sql.ScalarAsync("SS_PersonalEvents_CountGcal",
            SqlHelper.Param("@Email", email));
        return Convert.ToInt32(result);
    }

    public async Task<List<int>> GetGcalPersonalEventIdsAsync(string email)
    {
        return await _sql.QueryAsync("SS_PersonalEvents_GetGcalEventIds", r => r.GetInt32(0),
            SqlHelper.Param("@Email", email));
    }

    public async Task<bool> ClassEventExistsAsync(string email, int courseId, DateTime from, DateTime to)
    {
        var result = await _sql.ScalarAsync("SS_ClassEvents_Exists",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@From", from),
            SqlHelper.Param("@To", to));
        return Convert.ToInt32(result) == 1;
    }

    public async Task<Instructor?> FindInstructorByNameAsync(string name)
    {
        return await _sql.QuerySingleAsync("SS_Instructors_FindByName", r => new Instructor
        {
            InstructorId = r.GetInt32(r.GetOrdinal("InstructorId")),
            InstructorName = r.GetString(r.GetOrdinal("InstructorName"))
        }, SqlHelper.Param("@Name", name));
    }

    public async Task<int> CreateInstructorAsync(string name)
    {
        var result = await _sql.ScalarAsync("SS_Instructors_Create",
            SqlHelper.Param("@Name", name));
        return Convert.ToInt32(result);
    }

    public async Task<ExamBasicRow?> FindExamByCourseAndSessionAsync(string email, int courseId, string session)
    {
        return await _sql.QuerySingleAsync("SS_Exams_FindByCourseAndSession", r => new ExamBasicRow
        {
            ExamId = r.GetInt32(r.GetOrdinal("ExamId")),
            CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
            Date = r.GetDateTime(r.GetOrdinal("Date")),
            Time = r.GetTimeSpan(r.GetOrdinal("Time")),
            Session = r.GetString(r.GetOrdinal("Session")),
            Duration = r.IsDBNull(r.GetOrdinal("Duration")) ? null : r.GetInt32(r.GetOrdinal("Duration")),
            IsTakingExam = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsTakingExam")))
        }, SqlHelper.Param("@Email", email), SqlHelper.Param("@CourseId", courseId), SqlHelper.Param("@Session", session));
    }

    public async Task UpdateExamFullAsync(int examId, DateTime date, TimeSpan time, int? duration)
    {
        await _sql.ExecuteAsync("SS_Exams_UpdateFull",
            SqlHelper.Param("@ExamId", examId),
            SqlHelper.Param("@Date", date),
            SqlHelper.Param("@Time", time, SqlDbType.Time),
            SqlHelper.Param("@Duration", duration));
    }

    public async Task<List<Course>> GetAllCoursesAsync()
    {
        return await _sql.QueryAsync("SS_Courses_GetAll", MapCourse);
    }

    public async Task<bool> CourseExistsAsync(int courseId)
    {
        var result = await _sql.ScalarAsync("SS_Courses_Exists",
            SqlHelper.Param("@CourseId", courseId));
        return Convert.ToInt32(result) == 1;
    }

    public async Task<List<(int CourseId, string CourseName)>> GetUserCoursesWithNameAsync(string email)
    {
        return await _sql.QueryAsync("SS_UserCourses_GetWithCourseName",
            r => (r.GetInt32(r.GetOrdinal("CourseId")), r.GetString(r.GetOrdinal("CourseName"))),
            SqlHelper.Param("@Email", email));
    }

    public async Task ReassignClassEventsCourseAsync(string email, int fromCourseId, int toCourseId)
    {
        await _sql.ExecuteAsync("SS_ClassEvents_ReassignCourse",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@FromCourseId", fromCourseId),
            SqlHelper.Param("@ToCourseId", toCourseId));
    }

    public async Task ReassignExamsCourseAsync(int fromCourseId, int toCourseId)
    {
        await _sql.ExecuteAsync("SS_Exams_ReassignCourse",
            SqlHelper.Param("@FromCourseId", fromCourseId),
            SqlHelper.Param("@ToCourseId", toCourseId));
    }

    public async Task ReassignTasksCourseAsync(string email, int fromCourseId, int toCourseId)
    {
        await _sql.ExecuteAsync("SS_Tasks_ReassignCourse",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@FromCourseId", fromCourseId),
            SqlHelper.Param("@ToCourseId", toCourseId));
    }

    public async Task<bool> OtherUsersEnrolledAsync(int courseId, string excludeEmail)
    {
        var result = await _sql.ScalarAsync("SS_UserCourses_OtherUsersExist",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@ExcludeEmail", excludeEmail));
        return Convert.ToInt32(result) == 1;
    }

    public async Task DeleteCourseAsync(int courseId)
    {
        await _sql.ExecuteAsync("SS_Courses_Delete",
            SqlHelper.Param("@CourseId", courseId));
    }

    public async Task<List<string>> GetUsersForRuppinetSyncAsync(DateTime cutoff)
    {
        return await _sql.QueryAsync("SS_Users_GetForRuppinetSync", r => r.GetString(0),
            SqlHelper.Param("@Cutoff", cutoff));
    }

    public async Task UpdateLastCalendarSyncAsync(string email, DateTime lastSync)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateLastCalendarSync",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@LastCalendarSync", lastSync));
    }

    public async Task UpdateLastRuppinetSyncAsync(string email, DateTime lastSync)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateLastRuppinetSync",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@LastRuppinetSync", lastSync));
    }

    // =====================================================
    // MOODLE INTEGRATION
    // =====================================================

    public async Task UpdateMoodleFieldsAsync(string email, string? moodleToken, DateTime? lastMoodleSync)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateMoodleFields",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@MoodleToken", moodleToken),
            SqlHelper.Param("@LastMoodleSync", lastMoodleSync));
    }

    public async Task ClearMoodleAsync(string email)
    {
        await _sql.ExecuteAsync("SS_Users_ClearMoodle",
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<string>> GetUsersForMoodleSyncAsync(DateTime cutoff)
    {
        return await _sql.QueryAsync("SS_Users_GetForMoodleSync", r => r.GetString(0),
            SqlHelper.Param("@Cutoff", cutoff));
    }

    public async Task UpdateLastMoodleSyncAsync(string email, DateTime lastSync)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateLastMoodleSync",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@LastMoodleSync", lastSync));
    }

    public async Task<MoodleTaskMatch?> FindTaskByMoodleIdAsync(string email, string moodleId)
    {
        return await _sql.QuerySingleAsync("SS_Tasks_FindByMoodleId",
            r => new MoodleTaskMatch
            {
                TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
                CourseId = r.GetInt32(r.GetOrdinal("CourseId")),
                Title = r.GetString(r.GetOrdinal("Title")),
                DueDate = r.IsDBNull(r.GetOrdinal("DueDate")) ? null : r.GetDateTime(r.GetOrdinal("DueDate")),
                IsCompleted = Convert.ToBoolean(r.GetValue(r.GetOrdinal("IsCompleted")))
            },
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@MoodleId", moodleId));
    }

    public class MoodleTaskMatch
    {
        public int TaskId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
    }

    public async Task<int> CreateTaskWithMoodleIdAsync(int courseId, string email, string title, string type,
        decimal? estimatedHours, DateTime? dueDate, string? priority, string? moodleId)
    {
        var result = await _sql.ScalarAsync("SS_Tasks_CreateWithMoodleId",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@Title", title),
            SqlHelper.Param("@Type", type),
            SqlHelper.Param("@EstimatedHours", estimatedHours),
            SqlHelper.Param("@DueDate", dueDate),
            SqlHelper.Param("@Priority", priority),
            SqlHelper.Param("@MoodleId", moodleId));
        return Convert.ToInt32(result);
    }

    public async Task UpdateComposioIdAsync(string email, string? composioId)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateComposioId",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@ComposioConnectedAccountId", composioId));
    }

    public async Task UpdateCourseInstructorAsync(int courseId, int instructorId)
    {
        await _sql.ExecuteAsync("SS_Courses_UpdateInstructor",
            SqlHelper.Param("@CourseId", courseId),
            SqlHelper.Param("@InstructorId", instructorId));
    }

    public async Task<int> CountTasksByUserAsync(string email)
    {
        var result = await _sql.ScalarAsync("SS_Tasks_CountByUser",
            SqlHelper.Param("@Email", email));
        return Convert.ToInt32(result);
    }

    public async Task<(int ApprovedCount, int RemovedPast)> ApproveTaskEventsAsync(int taskId, string email, DateTime now)
    {
        int approved = 0, removed = 0;
        await _sql.QueryMultipleAsync("SS_TaskEvents_Approve", reader =>
        {
            if (reader.Read())
            {
                approved = reader.GetInt32(reader.GetOrdinal("ApprovedCount"));
                removed = reader.GetInt32(reader.GetOrdinal("RemovedPast"));
            }
        },
        SqlHelper.Param("@TaskId", taskId),
        SqlHelper.Param("@Email", email),
        SqlHelper.Param("@Now", now));
        return (approved, removed);
    }

    public async Task<List<string>> GetAllUserEmailsAsync()
    {
        return await _sql.QueryAsync("SS_Users_GetAllEmails", r => r.GetString(0));
    }

    public async Task UpdateGoogleTokenAsync(string email, string accessToken, DateTime lastSync)
    {
        await _sql.ExecuteAsync("SS_Users_UpdateGoogleToken",
            SqlHelper.Param("@Email", email),
            SqlHelper.Param("@GoogleCalendarAccessToken", accessToken),
            SqlHelper.Param("@LastCalendarSync", lastSync));
    }

    public async Task DisconnectGoogleCalendarAsync(string email)
    {
        await _sql.ExecuteAsync("SS_Users_DisconnectGoogleCalendar",
            SqlHelper.Param("@Email", email));
    }

    public async Task<List<string>> GetUsersByComposioIdAsync(string composioId)
    {
        return await _sql.QueryAsync("SS_Users_GetByComposioId", r => r.GetString(0),
            SqlHelper.Param("@ComposioConnectedAccountId", composioId));
    }

    public async Task ClearLastCalendarSyncAsync(string email)
    {
        await _sql.ExecuteAsync("SS_Users_ClearLastCalendarSync",
            SqlHelper.Param("@Email", email));
    }

    public async Task<(List<DashboardTaskRow> Tasks, List<DashboardTaskEventRow> TaskEvents)> GetIncompleteTasksWithEventsAsync(string email)
    {
        var tasks = new List<DashboardTaskRow>();
        var taskEvents = new List<DashboardTaskEventRow>();

        await _sql.QueryMultipleAsync("SS_Tasks_GetIncompleteWithEvents", reader =>
        {
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
        }, SqlHelper.Param("@Email", email));

        return (tasks, taskEvents);
    }

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

    private static TaskEventRow MapTaskEventRow(SqlDataReader r)
    {
        return new TaskEventRow
        {
            EventId = r.GetInt32(r.GetOrdinal("EventId")),
            Email = r.GetString(r.GetOrdinal("Email")),
            From = r.GetDateTime(r.GetOrdinal("From")),
            To = r.GetDateTime(r.GetOrdinal("To")),
            TaskId = r.GetInt32(r.GetOrdinal("TaskId")),
            Priority = r.IsDBNull(r.GetOrdinal("Priority")) ? null : r.GetString(r.GetOrdinal("Priority")),
            Status = r.IsDBNull(r.GetOrdinal("Status")) ? null : r.GetString(r.GetOrdinal("Status"))
        };
    }

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
}

// DTO class for course with enrollment info (returned by SS_Courses_GetByUser)
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
}

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

public class TaskEventInfo
{
    public int EventId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Priority { get; set; }
    public decimal? ActualHours { get; set; }
    public string? Status { get; set; }
}

public class SharedTaskInfo
{
    public int TaskId { get; set; }
    public string SharedStatus { get; set; } = null!;
    public string CreatedByEmail { get; set; } = null!;
    public List<SharedTaskMemberInfo> Members { get; set; } = new();
}

public class SharedTaskMemberInfo
{
    public string Email { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public string FullName { get; set; } = null!;
}

public class MLDataRow
{
    public decimal ActualHours { get; set; }
    public decimal EstimatedHours { get; set; }
}

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


public class PendingMemberForCourseRow
{
    public int TaskId { get; set; }
    public string Email { get; set; } = null!;
    public string ResponseStatus { get; set; } = null!;
    public DateTime? RespondedAt { get; set; }
    public string CreatedByEmail { get; set; } = null!;
    public string SharedStatus { get; set; } = null!;
}

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

public class UpcomingDeadlineTask
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string CourseName { get; set; } = null!;
}

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

public class TaskEventRow
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TaskId { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
}

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

public class MLCompletedTaskRow
{
    public int CourseId { get; set; }
    public double ActualHours { get; set; }
    public double EstimatedHours { get; set; }
}

public class WorkEventRow
{
    public int EventId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? WorkPlace { get; set; }
    public int? TravelTime { get; set; }
}

public class PersonalEventRow
{
    public int EventId { get; set; }
    public string? Email { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}

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

public class DashboardTaskEventRow
{
    public int TaskId { get; set; }
    public int EventId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Status { get; set; }
}
