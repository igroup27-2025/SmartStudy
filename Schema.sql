-- =====================================================
-- SmartStudy Database Schema for SQL Server
-- 18 tables with SmartStudy_ prefix
-- Matches ERD and EF Core model (SmartStudyDbContext)
-- Run this BEFORE SeedData.sql
-- =====================================================

-- ===== DROP TABLES (reverse dependency order) =====
IF OBJECT_ID('SmartStudy_Notifications','U')        IS NOT NULL DROP TABLE SmartStudy_Notifications;
IF OBJECT_ID('SmartStudy_SharedTaskMembers','U')     IS NOT NULL DROP TABLE SmartStudy_SharedTaskMembers;
IF OBJECT_ID('SmartStudy_SharedTasks','U')           IS NOT NULL DROP TABLE SmartStudy_SharedTasks;
IF OBJECT_ID('SmartStudy_Friendships','U')           IS NOT NULL DROP TABLE SmartStudy_Friendships;
IF OBJECT_ID('SmartStudy_FriendRequests','U')        IS NOT NULL DROP TABLE SmartStudy_FriendRequests;
IF OBJECT_ID('SmartStudy_StudyConnections','U')      IS NOT NULL DROP TABLE SmartStudy_StudyConnections;
IF OBJECT_ID('SmartStudy_TaskEvents','U')            IS NOT NULL DROP TABLE SmartStudy_TaskEvents;
IF OBJECT_ID('SmartStudy_ClassEvents','U')           IS NOT NULL DROP TABLE SmartStudy_ClassEvents;
IF OBJECT_ID('SmartStudy_WorkEvents','U')            IS NOT NULL DROP TABLE SmartStudy_WorkEvents;
IF OBJECT_ID('SmartStudy_PersonalEvents','U')        IS NOT NULL DROP TABLE SmartStudy_PersonalEvents;
IF OBJECT_ID('SmartStudy_Events','U')                IS NOT NULL DROP TABLE SmartStudy_Events;
IF OBJECT_ID('SmartStudy_Exams','U')                 IS NOT NULL DROP TABLE SmartStudy_Exams;
IF OBJECT_ID('SmartStudy_Tasks','U')                 IS NOT NULL DROP TABLE SmartStudy_Tasks;
IF OBJECT_ID('SmartStudy_UserCourses','U')           IS NOT NULL DROP TABLE SmartStudy_UserCourses;
IF OBJECT_ID('SmartStudy_Courses','U')               IS NOT NULL DROP TABLE SmartStudy_Courses;
IF OBJECT_ID('SmartStudy_SchedulingPreferences','U') IS NOT NULL DROP TABLE SmartStudy_SchedulingPreferences;
IF OBJECT_ID('SmartStudy_NotificationSettings','U')  IS NOT NULL DROP TABLE SmartStudy_NotificationSettings;
IF OBJECT_ID('SmartStudy_Users','U')                 IS NOT NULL DROP TABLE SmartStudy_Users;
IF OBJECT_ID('SmartStudy_Instructors','U')           IS NOT NULL DROP TABLE SmartStudy_Instructors;
GO

-- =====================================================
-- 1) INSTRUCTORS
-- =====================================================
CREATE TABLE SmartStudy_Instructors (
    InstructorId   INT            IDENTITY(1,1) NOT NULL,
    InstructorName NVARCHAR(200)  NOT NULL,
    CONSTRAINT PK_SmartStudy_Instructors PRIMARY KEY (InstructorId)
);
GO

-- =====================================================
-- 2) USERS
-- PK is Email (not surrogate int)
-- =====================================================
CREATE TABLE SmartStudy_Users (
    Email                      NVARCHAR(255)  NOT NULL,
    FirstName                  NVARCHAR(100)  NOT NULL,
    LastName                   NVARCHAR(100)  NOT NULL,
    [Password]                 NVARCHAR(255)  NOT NULL,
    ResetToken                 NVARCHAR(50)   NULL,
    ResetTokenExpiry           DATETIME2      NULL,
    AuthProvider               NVARCHAR(20)   NULL,
    OnboardingCompleted        BIT            NOT NULL DEFAULT 0,
    GoogleCalendarAccessToken  NVARCHAR(MAX)  NULL,
    GoogleCalendarRefreshToken NVARCHAR(MAX)  NULL,
    LastCalendarSync           DATETIME2      NULL,
    ComposioConnectedAccountId NVARCHAR(255)  NULL,
    RuppinetId                 NVARCHAR(20)   NULL,
    RuppinetPassword           NVARCHAR(500)  NULL,
    LastRuppinetSync           DATETIME2      NULL,
    MoodleToken                NVARCHAR(500)  NULL,
    LastMoodleSync             DATETIME2      NULL,
    CONSTRAINT PK_SmartStudy_Users PRIMARY KEY (Email)
);
GO

-- =====================================================
-- 3) NOTIFICATION SETTINGS
-- 1:1 with Users (same PK)
-- =====================================================
CREATE TABLE SmartStudy_NotificationSettings (
    Email                    NVARCHAR(255) NOT NULL,
    Notify_before_task       BIT           NOT NULL DEFAULT 0,
    Daily_morning_summary    BIT           NOT NULL DEFAULT 0,
    Weekly_plan_reminder     BIT           NOT NULL DEFAULT 0,
    Enable_push_notification BIT           NOT NULL DEFAULT 0,
    Quiet_hours_start        TIME          NULL,
    Quiet_hours_end          TIME          NULL,
    CONSTRAINT PK_SmartStudy_NotificationSettings PRIMARY KEY (Email),
    CONSTRAINT FK_NotificationSettings_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
);
GO

-- =====================================================
-- 4) SCHEDULING PREFERENCES
-- 1:1 with Users (same PK)
-- =====================================================
CREATE TABLE SmartStudy_SchedulingPreferences (
    Email                     NVARCHAR(255) NOT NULL,
    MaxDailyStudyHours        FLOAT         NOT NULL DEFAULT 6.0,
    MaxContinuousMinutes      INT           NOT NULL DEFAULT 90,
    DayStartHour              INT           NOT NULL DEFAULT 8,
    DayEndHour                INT           NOT NULL DEFAULT 22,
    SleepHoursPerDay          FLOAT         NOT NULL DEFAULT 8.0,
    LunchBreakStart           TIME          NULL,
    LunchBreakEnd             TIME          NULL,
    BreakDurationMinutes      INT           NOT NULL DEFAULT 15,
    DefaultTaskEstimatedHours FLOAT         NOT NULL DEFAULT 4.0,
    MaxDailyTotalHours        FLOAT         NOT NULL DEFAULT 14.0,
    ExamPrepHoursPerDay       FLOAT         NOT NULL DEFAULT 5.0,
    ExamPrepDays              INT           NOT NULL DEFAULT 3,
    CONSTRAINT PK_SmartStudy_SchedulingPreferences PRIMARY KEY (Email),
    CONSTRAINT FK_SchedulingPreferences_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
);
GO

-- =====================================================
-- 5) COURSES
-- CourseId is manually assigned (not identity)
-- =====================================================
CREATE TABLE SmartStudy_Courses (
    CourseId                  INT            NOT NULL,
    CourseName                NVARCHAR(200)  NOT NULL,
    WeeklyHours               DECIMAL(4,1)   NULL,
    Credits                   DECIMAL(4,1)   NULL,
    Semester                  NVARCHAR(50)   NULL,
    InstructorId              INT            NULL,
    DefaultTaskEstimatedHours FLOAT          NULL,
    ExamPrepHoursPerDay       FLOAT          NULL,
    ExamPrepDays              INT            NULL,
    CONSTRAINT PK_SmartStudy_Courses PRIMARY KEY (CourseId),
    CONSTRAINT FK_Courses_Instructors FOREIGN KEY (InstructorId)
        REFERENCES SmartStudy_Instructors(InstructorId) ON DELETE SET NULL
);
GO

-- =====================================================
-- 6) USER-COURSES (junction table, N:N enrollment)
-- Composite PK (Email, CourseId)
-- =====================================================
CREATE TABLE SmartStudy_UserCourses (
    Email              NVARCHAR(255) NOT NULL,
    CourseId           INT           NOT NULL,
    StudyPartnerEmail  NVARCHAR(255) NULL,
    SharedByDefault    BIT           NOT NULL DEFAULT 0,
    CourseShareApproved BIT          NOT NULL DEFAULT 0,
    CONSTRAINT PK_SmartStudy_UserCourses PRIMARY KEY (Email, CourseId),
    CONSTRAINT FK_UserCourses_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE,
    CONSTRAINT FK_UserCourses_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 7) EXAMS
-- =====================================================
CREATE TABLE SmartStudy_Exams (
    ExamId       INT          IDENTITY(1,1) NOT NULL,
    CourseId     INT          NOT NULL,
    [Date]       DATETIME2    NOT NULL,
    [Time]       TIME         NOT NULL,
    [Session]    NVARCHAR(10) NOT NULL,
    Duration     INT          NULL,
    IsTakingExam BIT          NOT NULL DEFAULT 1,
    CONSTRAINT PK_SmartStudy_Exams PRIMARY KEY (ExamId),
    CONSTRAINT FK_Exams_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 8) TASKS
-- =====================================================
CREATE TABLE SmartStudy_Tasks (
    TaskId           INT            IDENTITY(1,1) NOT NULL,
    CourseId         INT            NOT NULL,
    Email            NVARCHAR(255)  NOT NULL,
    Title            NVARCHAR(200)  NOT NULL,
    [Type]           NVARCHAR(50)   NOT NULL,
    EstimatedHours   DECIMAL(5,2)   NULL,
    DueDate          DATETIME2      NULL,
    IsCompleted      BIT            NOT NULL DEFAULT 0,
    [Priority]       NVARCHAR(20)   NULL,
    ActualHours      DECIMAL(5,2)   NULL,
    ParentTaskId     INT            NULL,
    AllowSplitting   BIT            NOT NULL DEFAULT 0,
    IsManuallyPinned BIT            NOT NULL DEFAULT 0,
    IsManualPriority BIT            NOT NULL DEFAULT 0,
    MoodleId         NVARCHAR(100)  NULL,
    CONSTRAINT PK_SmartStudy_Tasks PRIMARY KEY (TaskId),
    CONSTRAINT FK_Tasks_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE,
    CONSTRAINT FK_Tasks_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION,
    CONSTRAINT FK_Tasks_ParentTask FOREIGN KEY (ParentTaskId)
        REFERENCES SmartStudy_Tasks(TaskId) ON DELETE NO ACTION
);
GO

-- =====================================================
-- 9) EVENTS (base table — TPT inheritance)
-- Four subtypes: ClassEvents, TaskEvents, WorkEvents, PersonalEvents
-- =====================================================
CREATE TABLE SmartStudy_Events (
    EventId           INT            IDENTITY(1,1) NOT NULL,
    Email             NVARCHAR(255)  NOT NULL,
    [From]            DATETIME2      NOT NULL,
    [To]              DATETIME2      NOT NULL,
    Recurring         BIT            NOT NULL DEFAULT 0,
    RecurrenceEndDate DATETIME2      NULL,
    CONSTRAINT PK_SmartStudy_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
);
GO

-- =====================================================
-- 10) CLASS EVENTS (TPT subtype)
-- =====================================================
CREATE TABLE SmartStudy_ClassEvents (
    EventId  INT           NOT NULL,
    CourseId INT           NOT NULL,
    Location NVARCHAR(200) NULL,
    Duration DECIMAL(5,2)  NULL,
    CONSTRAINT PK_SmartStudy_ClassEvents PRIMARY KEY (EventId),
    CONSTRAINT FK_ClassEvents_Events FOREIGN KEY (EventId)
        REFERENCES SmartStudy_Events(EventId) ON DELETE CASCADE,
    CONSTRAINT FK_ClassEvents_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 11) TASK EVENTS (TPT subtype)
-- Links a calendar block to a specific task
-- =====================================================
CREATE TABLE SmartStudy_TaskEvents (
    EventId     INT           NOT NULL,
    TaskId      INT           NOT NULL,
    [Priority]  NVARCHAR(50)  NULL,
    ActualHours DECIMAL(5,2)  NULL,
    [Status]    NVARCHAR(50)  NULL,
    CONSTRAINT PK_SmartStudy_TaskEvents PRIMARY KEY (EventId),
    CONSTRAINT FK_TaskEvents_Events FOREIGN KEY (EventId)
        REFERENCES SmartStudy_Events(EventId) ON DELETE CASCADE,
    CONSTRAINT FK_TaskEvents_Tasks FOREIGN KEY (TaskId)
        REFERENCES SmartStudy_Tasks(TaskId) ON DELETE NO ACTION
);
GO

-- =====================================================
-- 12) WORK EVENTS (TPT subtype)
-- =====================================================
CREATE TABLE SmartStudy_WorkEvents (
    EventId    INT           NOT NULL,
    TravelTime INT           NULL,
    WorkPlace  NVARCHAR(200) NULL,
    CONSTRAINT PK_SmartStudy_WorkEvents PRIMARY KEY (EventId),
    CONSTRAINT FK_WorkEvents_Events FOREIGN KEY (EventId)
        REFERENCES SmartStudy_Events(EventId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 13) PERSONAL EVENTS (TPT subtype)
-- =====================================================
CREATE TABLE SmartStudy_PersonalEvents (
    EventId       INT            NOT NULL,
    [Type]        NVARCHAR(50)   NULL,
    [Description] NVARCHAR(MAX)  NULL,
    CONSTRAINT PK_SmartStudy_PersonalEvents PRIMARY KEY (EventId),
    CONSTRAINT FK_PersonalEvents_Events FOREIGN KEY (EventId)
        REFERENCES SmartStudy_Events(EventId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 14) FRIEND REQUESTS (invitation lifecycle)
-- =====================================================
CREATE TABLE SmartStudy_FriendRequests (
    RequestId      INT           IDENTITY(1,1) NOT NULL,
    RequesterEmail NVARCHAR(255) NOT NULL,
    AddresseeEmail NVARCHAR(255) NOT NULL,
    [Status]       NVARCHAR(20)  NOT NULL DEFAULT N'Pending',
    RequestedAt    DATETIME2     NOT NULL,
    RespondedAt    DATETIME2     NULL,
    CONSTRAINT PK_SmartStudy_FriendRequests PRIMARY KEY (RequestId),
    CONSTRAINT FK_FriendRequests_Requester FOREIGN KEY (RequesterEmail)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE,
    CONSTRAINT FK_FriendRequests_Addressee FOREIGN KEY (AddresseeEmail)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION,
    CONSTRAINT CK_FriendRequest_NotSelf CHECK (RequesterEmail <> AddresseeEmail)
);
GO

-- =====================================================
-- 15) FRIENDSHIPS (confirmed friends)
-- Email1 < Email2 alphabetically (normalized pair)
-- =====================================================
CREATE TABLE SmartStudy_Friendships (
    FriendshipId INT           IDENTITY(1,1) NOT NULL,
    Email1       NVARCHAR(255) NOT NULL,
    Email2       NVARCHAR(255) NOT NULL,
    CreatedAt    DATETIME2     NOT NULL,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CONSTRAINT PK_SmartStudy_Friendships PRIMARY KEY (FriendshipId),
    CONSTRAINT FK_Friendships_User1 FOREIGN KEY (Email1)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE,
    CONSTRAINT FK_Friendships_User2 FOREIGN KEY (Email2)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION,
    CONSTRAINT CK_Friendship_NotSelf CHECK (Email1 <> Email2)
);
GO

-- =====================================================
-- 16) SHARED TASKS (1:1 with Tasks)
-- =====================================================
CREATE TABLE SmartStudy_SharedTasks (
    TaskId         INT           NOT NULL,
    CreatedByEmail NVARCHAR(255) NOT NULL,
    CreatedAt      DATETIME2     NOT NULL,
    SharedStatus   NVARCHAR(20)  NOT NULL DEFAULT N'Draft',
    CONSTRAINT PK_SmartStudy_SharedTasks PRIMARY KEY (TaskId),
    CONSTRAINT FK_SharedTasks_Task FOREIGN KEY (TaskId)
        REFERENCES SmartStudy_Tasks(TaskId) ON DELETE CASCADE,
    CONSTRAINT FK_SharedTasks_CreatedBy FOREIGN KEY (CreatedByEmail)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION
);
GO

-- =====================================================
-- 17) SHARED TASK MEMBERS
-- Composite PK (TaskId, Email)
-- =====================================================
CREATE TABLE SmartStudy_SharedTaskMembers (
    TaskId         INT           NOT NULL,
    Email          NVARCHAR(255) NOT NULL,
    ResponseStatus NVARCHAR(20)  NOT NULL DEFAULT N'Pending',
    RespondedAt    DATETIME2     NULL,
    CopyTaskId     INT           NULL,      -- partner's copy of the shared task
    CONSTRAINT PK_SmartStudy_SharedTaskMembers PRIMARY KEY (TaskId, Email),
    CONSTRAINT FK_SharedTaskMembers_SharedTask FOREIGN KEY (TaskId)
        REFERENCES SmartStudy_SharedTasks(TaskId) ON DELETE CASCADE,
    CONSTRAINT FK_SharedTaskMembers_User FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION
);
GO

-- =====================================================
-- 18) NOTIFICATIONS
-- =====================================================
CREATE TABLE SmartStudy_Notifications (
    NotificationId    INT           IDENTITY(1,1) NOT NULL,
    Email             NVARCHAR(255) NOT NULL,
    [Type]            NVARCHAR(50)  NOT NULL,
    Title             NVARCHAR(200) NOT NULL,
    [Message]         NVARCHAR(1000) NOT NULL,
    IsRead            BIT           NOT NULL DEFAULT 0,
    CreatedAt         DATETIME2     NOT NULL,
    RelatedEntityId   INT           NULL,
    RelatedEntityType NVARCHAR(50)  NULL,
    CONSTRAINT PK_SmartStudy_Notifications PRIMARY KEY (NotificationId),
    CONSTRAINT FK_Notifications_User FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
);
GO

-- =====================================================
-- INDEXES
-- =====================================================

-- Events
CREATE NONCLUSTERED INDEX IX_Events_Email_From ON SmartStudy_Events(Email, [From]);

-- Tasks
CREATE NONCLUSTERED INDEX IX_Tasks_CourseId_DueDate ON SmartStudy_Tasks(CourseId, DueDate);
CREATE NONCLUSTERED INDEX IX_Tasks_Email ON SmartStudy_Tasks(Email);
CREATE NONCLUSTERED INDEX IX_Tasks_ParentTaskId ON SmartStudy_Tasks(ParentTaskId);

-- Exams
CREATE NONCLUSTERED INDEX IX_Exams_CourseId_Date ON SmartStudy_Exams(CourseId, [Date]);

-- Event subtypes
CREATE NONCLUSTERED INDEX IX_TaskEvents_TaskId ON SmartStudy_TaskEvents(TaskId);
CREATE NONCLUSTERED INDEX IX_ClassEvents_CourseId ON SmartStudy_ClassEvents(CourseId);

-- UserCourses
CREATE NONCLUSTERED INDEX IX_UserCourses_CourseId ON SmartStudy_UserCourses(CourseId);

-- Courses
CREATE NONCLUSTERED INDEX IX_Courses_InstructorId ON SmartStudy_Courses(InstructorId);

-- FriendRequests
CREATE UNIQUE NONCLUSTERED INDEX IX_FriendRequests_Pending ON SmartStudy_FriendRequests(RequesterEmail, AddresseeEmail) WHERE [Status] = 'Pending';
CREATE NONCLUSTERED INDEX IX_FriendRequests_Addressee ON SmartStudy_FriendRequests(AddresseeEmail);

-- Friendships
CREATE UNIQUE NONCLUSTERED INDEX IX_Friendships_Pair ON SmartStudy_Friendships(Email1, Email2);

-- SharedTaskMembers
CREATE NONCLUSTERED INDEX IX_SharedTaskMembers_Email ON SmartStudy_SharedTaskMembers(Email);
CREATE NONCLUSTERED INDEX IX_SharedTaskMembers_CopyTaskId ON SmartStudy_SharedTaskMembers(CopyTaskId) WHERE CopyTaskId IS NOT NULL;

-- Notifications
CREATE NONCLUSTERED INDEX IX_Notifications_Email_CreatedAt ON SmartStudy_Notifications(Email, CreatedAt DESC);
GO

PRINT 'SmartStudy schema created successfully (18 tables).';
GO

-- =====================================================
-- STORED PROCEDURES (SS_ prefix)
-- =====================================================

-- ===== USERS =====

IF OBJECT_ID('SS_Users_GetByEmail','P') IS NOT NULL DROP PROCEDURE SS_Users_GetByEmail;
GO
CREATE PROCEDURE SS_Users_GetByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email, FirstName, LastName, [Password], ResetToken, ResetTokenExpiry,
           AuthProvider, OnboardingCompleted,
           GoogleCalendarAccessToken, GoogleCalendarRefreshToken, LastCalendarSync,
           ComposioConnectedAccountId, RuppinetId, RuppinetPassword, LastRuppinetSync,
           MoodleToken, LastMoodleSync
    FROM SmartStudy_Users
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_ExistsByEmail','P') IS NOT NULL DROP PROCEDURE SS_Users_ExistsByEmail;
GO
CREATE PROCEDURE SS_Users_ExistsByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM SmartStudy_Users WHERE Email = @Email) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_Users_Create','P') IS NOT NULL DROP PROCEDURE SS_Users_Create;
GO
CREATE PROCEDURE SS_Users_Create
    @Email NVARCHAR(255),
    @FirstName NVARCHAR(100),
    @LastName NVARCHAR(100),
    @Password NVARCHAR(255),
    @AuthProvider NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Users (Email, FirstName, LastName, [Password], AuthProvider, OnboardingCompleted)
    VALUES (@Email, @FirstName, @LastName, @Password, @AuthProvider, 0);
END
GO

IF OBJECT_ID('SS_Users_UpdateProfile','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateProfile;
GO
CREATE PROCEDURE SS_Users_UpdateProfile
    @Email NVARCHAR(255),
    @FirstName NVARCHAR(100) = NULL,
    @LastName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET FirstName = ISNULL(@FirstName, FirstName),
        LastName = ISNULL(@LastName, LastName)
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_UpdateResetToken','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateResetToken;
GO
CREATE PROCEDURE SS_Users_UpdateResetToken
    @Email NVARCHAR(255),
    @ResetToken NVARCHAR(50),
    @ResetTokenExpiry DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET ResetToken = @ResetToken, ResetTokenExpiry = @ResetTokenExpiry
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_ResetPassword','P') IS NOT NULL DROP PROCEDURE SS_Users_ResetPassword;
GO
CREATE PROCEDURE SS_Users_ResetPassword
    @Email NVARCHAR(255),
    @NewPassword NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET [Password] = @NewPassword, ResetToken = NULL, ResetTokenExpiry = NULL
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_SetOnboardingComplete','P') IS NOT NULL DROP PROCEDURE SS_Users_SetOnboardingComplete;
GO
CREATE PROCEDURE SS_Users_SetOnboardingComplete
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users SET OnboardingCompleted = 1 WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_UpdateRuppinetFields','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateRuppinetFields;
GO
CREATE PROCEDURE SS_Users_UpdateRuppinetFields
    @Email NVARCHAR(255),
    @RuppinetId NVARCHAR(20),
    @RuppinetPassword NVARCHAR(500),
    @LastRuppinetSync DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET RuppinetId = @RuppinetId, RuppinetPassword = @RuppinetPassword, LastRuppinetSync = @LastRuppinetSync
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_ClearRuppinet','P') IS NOT NULL DROP PROCEDURE SS_Users_ClearRuppinet;
GO
CREATE PROCEDURE SS_Users_ClearRuppinet
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET RuppinetId = NULL, RuppinetPassword = NULL, LastRuppinetSync = NULL
    WHERE Email = @Email;
END
GO


-- ===== NOTIFICATION SETTINGS =====

IF OBJECT_ID('SS_NotifSettings_GetByEmail','P') IS NOT NULL DROP PROCEDURE SS_NotifSettings_GetByEmail;
GO
CREATE PROCEDURE SS_NotifSettings_GetByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email, Notify_before_task, Daily_morning_summary, Weekly_plan_reminder,
           Enable_push_notification, Quiet_hours_start, Quiet_hours_end
    FROM SmartStudy_NotificationSettings
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_NotifSettings_Upsert','P') IS NOT NULL DROP PROCEDURE SS_NotifSettings_Upsert;
GO
CREATE PROCEDURE SS_NotifSettings_Upsert
    @Email NVARCHAR(255),
    @NotifyBeforeTask BIT,
    @DailyMorningSummary BIT,
    @WeeklyPlanReminder BIT,
    @EnablePushNotification BIT,
    @QuietHoursStart TIME = NULL,
    @QuietHoursEnd TIME = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM SmartStudy_NotificationSettings WHERE Email = @Email)
        UPDATE SmartStudy_NotificationSettings
        SET Notify_before_task = @NotifyBeforeTask,
            Daily_morning_summary = @DailyMorningSummary,
            Weekly_plan_reminder = @WeeklyPlanReminder,
            Enable_push_notification = @EnablePushNotification,
            Quiet_hours_start = @QuietHoursStart,
            Quiet_hours_end = @QuietHoursEnd
        WHERE Email = @Email;
    ELSE
        INSERT INTO SmartStudy_NotificationSettings (Email, Notify_before_task, Daily_morning_summary, Weekly_plan_reminder, Enable_push_notification, Quiet_hours_start, Quiet_hours_end)
        VALUES (@Email, @NotifyBeforeTask, @DailyMorningSummary, @WeeklyPlanReminder, @EnablePushNotification, @QuietHoursStart, @QuietHoursEnd);
END
GO

IF OBJECT_ID('SS_NotifSettings_CreateDefault','P') IS NOT NULL DROP PROCEDURE SS_NotifSettings_CreateDefault;
GO
CREATE PROCEDURE SS_NotifSettings_CreateDefault
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM SmartStudy_NotificationSettings WHERE Email = @Email)
        INSERT INTO SmartStudy_NotificationSettings (Email) VALUES (@Email);
END
GO

-- ===== SCHEDULING PREFERENCES =====

IF OBJECT_ID('SS_SchedPrefs_GetByEmail','P') IS NOT NULL DROP PROCEDURE SS_SchedPrefs_GetByEmail;
GO
CREATE PROCEDURE SS_SchedPrefs_GetByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email, MaxDailyStudyHours, MaxContinuousMinutes, DayStartHour, DayEndHour,
           SleepHoursPerDay, LunchBreakStart, LunchBreakEnd, BreakDurationMinutes,
           DefaultTaskEstimatedHours, MaxDailyTotalHours, ExamPrepHoursPerDay, ExamPrepDays
    FROM SmartStudy_SchedulingPreferences
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_SchedPrefs_Upsert','P') IS NOT NULL DROP PROCEDURE SS_SchedPrefs_Upsert;
GO
CREATE PROCEDURE SS_SchedPrefs_Upsert
    @Email NVARCHAR(255),
    @MaxDailyStudyHours FLOAT,
    @MaxContinuousMinutes INT,
    @DayStartHour INT,
    @DayEndHour INT,
    @SleepHoursPerDay FLOAT,
    @LunchBreakStart TIME = NULL,
    @LunchBreakEnd TIME = NULL,
    @BreakDurationMinutes INT = 15,
    @DefaultTaskEstimatedHours FLOAT = 4.0,
    @MaxDailyTotalHours FLOAT = 14.0,
    @ExamPrepHoursPerDay FLOAT = 5.0,
    @ExamPrepDays INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM SmartStudy_SchedulingPreferences WHERE Email = @Email)
        UPDATE SmartStudy_SchedulingPreferences
        SET MaxDailyStudyHours = @MaxDailyStudyHours, MaxContinuousMinutes = @MaxContinuousMinutes,
            DayStartHour = @DayStartHour, DayEndHour = @DayEndHour,
            SleepHoursPerDay = @SleepHoursPerDay, LunchBreakStart = @LunchBreakStart,
            LunchBreakEnd = @LunchBreakEnd, BreakDurationMinutes = @BreakDurationMinutes,
            DefaultTaskEstimatedHours = @DefaultTaskEstimatedHours, MaxDailyTotalHours = @MaxDailyTotalHours,
            ExamPrepHoursPerDay = @ExamPrepHoursPerDay, ExamPrepDays = @ExamPrepDays
        WHERE Email = @Email;
    ELSE
        INSERT INTO SmartStudy_SchedulingPreferences (Email, MaxDailyStudyHours, MaxContinuousMinutes, DayStartHour, DayEndHour, SleepHoursPerDay, LunchBreakStart, LunchBreakEnd, BreakDurationMinutes, DefaultTaskEstimatedHours, MaxDailyTotalHours, ExamPrepHoursPerDay, ExamPrepDays)
        VALUES (@Email, @MaxDailyStudyHours, @MaxContinuousMinutes, @DayStartHour, @DayEndHour, @SleepHoursPerDay, @LunchBreakStart, @LunchBreakEnd, @BreakDurationMinutes, @DefaultTaskEstimatedHours, @MaxDailyTotalHours, @ExamPrepHoursPerDay, @ExamPrepDays);
END
GO

-- ===== INSTRUCTORS =====

IF OBJECT_ID('SS_Instructors_GetAll','P') IS NOT NULL DROP PROCEDURE SS_Instructors_GetAll;
GO
CREATE PROCEDURE SS_Instructors_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT InstructorId, InstructorName FROM SmartStudy_Instructors;
END
GO

-- ===== COURSES =====

IF OBJECT_ID('SS_Courses_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Courses_GetByUser;
GO
CREATE PROCEDURE SS_Courses_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CourseId, c.CourseName, c.WeeklyHours, c.Credits, c.Semester,
           c.InstructorId, i.InstructorName,
           c.DefaultTaskEstimatedHours, c.ExamPrepHoursPerDay, c.ExamPrepDays,
           uc.StudyPartnerEmail, uc.SharedByDefault, uc.CourseShareApproved,
           (SELECT COUNT(*) FROM SmartStudy_Tasks t WHERE t.CourseId = c.CourseId AND t.Email = @Email) AS TaskCount,
           (SELECT COUNT(*) FROM SmartStudy_Exams e WHERE e.CourseId = c.CourseId) AS ExamCount
    FROM SmartStudy_UserCourses uc
    INNER JOIN SmartStudy_Courses c ON uc.CourseId = c.CourseId
    LEFT JOIN SmartStudy_Instructors i ON c.InstructorId = i.InstructorId
    WHERE uc.Email = @Email;
END
GO

IF OBJECT_ID('SS_Courses_GetById','P') IS NOT NULL DROP PROCEDURE SS_Courses_GetById;
GO
CREATE PROCEDURE SS_Courses_GetById
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CourseId, c.CourseName, c.WeeklyHours, c.Credits, c.Semester,
           c.InstructorId, i.InstructorName,
           c.DefaultTaskEstimatedHours, c.ExamPrepHoursPerDay, c.ExamPrepDays
    FROM SmartStudy_Courses c
    LEFT JOIN SmartStudy_Instructors i ON c.InstructorId = i.InstructorId
    WHERE c.CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_Courses_GetMaxId','P') IS NOT NULL DROP PROCEDURE SS_Courses_GetMaxId;
GO
CREATE PROCEDURE SS_Courses_GetMaxId
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(CourseId), 0) FROM SmartStudy_Courses;
END
GO

IF OBJECT_ID('SS_Courses_Create','P') IS NOT NULL DROP PROCEDURE SS_Courses_Create;
GO
CREATE PROCEDURE SS_Courses_Create
    @CourseId INT,
    @CourseName NVARCHAR(200),
    @WeeklyHours DECIMAL(4,1) = NULL,
    @Credits DECIMAL(4,1) = NULL,
    @Semester NVARCHAR(50) = NULL,
    @InstructorId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Courses (CourseId, CourseName, WeeklyHours, Credits, Semester, InstructorId)
    VALUES (@CourseId, @CourseName, @WeeklyHours, @Credits, @Semester, @InstructorId);
END
GO

IF OBJECT_ID('SS_Courses_Update','P') IS NOT NULL DROP PROCEDURE SS_Courses_Update;
GO
CREATE PROCEDURE SS_Courses_Update
    @CourseId INT,
    @CourseName NVARCHAR(200) = NULL,
    @WeeklyHours DECIMAL(4,1) = NULL,
    @Credits DECIMAL(4,1) = NULL,
    @Semester NVARCHAR(50) = NULL,
    @InstructorId INT = NULL,
    @DefaultTaskEstimatedHours FLOAT = NULL,
    @ExamPrepHoursPerDay FLOAT = NULL,
    @ExamPrepDays INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Courses
    SET CourseName = ISNULL(@CourseName, CourseName),
        WeeklyHours = ISNULL(@WeeklyHours, WeeklyHours),
        Credits = ISNULL(@Credits, Credits),
        Semester = ISNULL(@Semester, Semester),
        InstructorId = ISNULL(@InstructorId, InstructorId),
        DefaultTaskEstimatedHours = ISNULL(@DefaultTaskEstimatedHours, DefaultTaskEstimatedHours),
        ExamPrepHoursPerDay = ISNULL(@ExamPrepHoursPerDay, ExamPrepHoursPerDay),
        ExamPrepDays = ISNULL(@ExamPrepDays, ExamPrepDays)
    WHERE CourseId = @CourseId;
END
GO

-- ===== USER COURSES =====

IF OBJECT_ID('SS_UserCourses_Exists','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_Exists;
GO
CREATE PROCEDURE SS_UserCourses_Exists
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM SmartStudy_UserCourses WHERE Email = @Email AND CourseId = @CourseId) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_UserCourses_Create','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_Create;
GO
CREATE PROCEDURE SS_UserCourses_Create
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_UserCourses (Email, CourseId, SharedByDefault, CourseShareApproved)
    VALUES (@Email, @CourseId, 0, 0);
END
GO

IF OBJECT_ID('SS_UserCourses_Delete','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_Delete;
GO
CREATE PROCEDURE SS_UserCourses_Delete
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM SmartStudy_UserCourses WHERE Email = @Email AND CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_UserCourses_UpdatePartner','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_UpdatePartner;
GO
CREATE PROCEDURE SS_UserCourses_UpdatePartner
    @Email NVARCHAR(255),
    @CourseId INT,
    @StudyPartnerEmail NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_UserCourses SET StudyPartnerEmail = @StudyPartnerEmail
    WHERE Email = @Email AND CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_UserCourses_UpdateSharedByDefault','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_UpdateSharedByDefault;
GO
CREATE PROCEDURE SS_UserCourses_UpdateSharedByDefault
    @Email NVARCHAR(255),
    @CourseId INT,
    @SharedByDefault BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_UserCourses SET SharedByDefault = @SharedByDefault
    WHERE Email = @Email AND CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_UserCourses_GetCourseIdsByEmail','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_GetCourseIdsByEmail;
GO
CREATE PROCEDURE SS_UserCourses_GetCourseIdsByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CourseId FROM SmartStudy_UserCourses WHERE Email = @Email;
END
GO

-- ===== EXAMS =====

IF OBJECT_ID('SS_Exams_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Exams_GetByUser;
GO
CREATE PROCEDURE SS_Exams_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.CourseId, c.CourseName, e.[Date], e.[Time], e.[Session], e.Duration, e.IsTakingExam
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_Courses c ON e.CourseId = c.CourseId
    INNER JOIN SmartStudy_UserCourses uc ON e.CourseId = uc.CourseId AND uc.Email = @Email
    ORDER BY e.[Date];
END
GO

IF OBJECT_ID('SS_Exams_GetById','P') IS NOT NULL DROP PROCEDURE SS_Exams_GetById;
GO
CREATE PROCEDURE SS_Exams_GetById
    @ExamId INT,
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.CourseId, c.CourseName, e.[Date], e.[Time], e.[Session], e.Duration, e.IsTakingExam
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_Courses c ON e.CourseId = c.CourseId
    INNER JOIN SmartStudy_UserCourses uc ON e.CourseId = uc.CourseId AND uc.Email = @Email
    WHERE e.ExamId = @ExamId;
END
GO

IF OBJECT_ID('SS_Exams_Create','P') IS NOT NULL DROP PROCEDURE SS_Exams_Create;
GO
CREATE PROCEDURE SS_Exams_Create
    @CourseId INT,
    @Date DATETIME2,
    @Time TIME,
    @Session NVARCHAR(10),
    @Duration INT = NULL,
    @IsTakingExam BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Exams (CourseId, [Date], [Time], [Session], Duration, IsTakingExam)
    VALUES (@CourseId, @Date, @Time, @Session, @Duration, @IsTakingExam);
    SELECT SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('SS_Exams_Update','P') IS NOT NULL DROP PROCEDURE SS_Exams_Update;
GO
CREATE PROCEDURE SS_Exams_Update
    @ExamId INT,
    @CourseId INT = NULL,
    @Date DATETIME2 = NULL,
    @Time TIME = NULL,
    @Session NVARCHAR(10) = NULL,
    @Duration INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Exams
    SET CourseId = ISNULL(@CourseId, CourseId),
        [Date] = ISNULL(@Date, [Date]),
        [Time] = ISNULL(@Time, [Time]),
        [Session] = ISNULL(@Session, [Session]),
        Duration = ISNULL(@Duration, Duration)
    WHERE ExamId = @ExamId;
END
GO

IF OBJECT_ID('SS_Exams_ToggleTaking','P') IS NOT NULL DROP PROCEDURE SS_Exams_ToggleTaking;
GO
CREATE PROCEDURE SS_Exams_ToggleTaking
    @ExamId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Exams SET IsTakingExam = CASE WHEN IsTakingExam = 1 THEN 0 ELSE 1 END
    WHERE ExamId = @ExamId;
END
GO

IF OBJECT_ID('SS_Exams_Delete','P') IS NOT NULL DROP PROCEDURE SS_Exams_Delete;
GO
CREATE PROCEDURE SS_Exams_Delete
    @ExamId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CourseId INT, @ExamDate DATETIME2;
    SELECT @CourseId = CourseId, @ExamDate = [Date]
    FROM SmartStudy_Exams WHERE ExamId = @ExamId;

    IF @CourseId IS NOT NULL
    BEGIN
        DECLARE @StudyTaskIds TABLE (TaskId INT PRIMARY KEY);
        INSERT INTO @StudyTaskIds
        SELECT TaskId FROM SmartStudy_Tasks
        WHERE CourseId = @CourseId
          AND [Type] = 'Study for exam'
          AND CAST(DueDate AS DATE) = CAST(@ExamDate AS DATE);

        DECLARE @StudyEventIds TABLE (EventId INT PRIMARY KEY);
        INSERT INTO @StudyEventIds
        SELECT te.EventId FROM SmartStudy_TaskEvents te
        WHERE te.TaskId IN (SELECT TaskId FROM @StudyTaskIds);

        DELETE FROM SmartStudy_TaskEvents WHERE EventId IN (SELECT EventId FROM @StudyEventIds);
        DELETE FROM SmartStudy_Events     WHERE EventId IN (SELECT EventId FROM @StudyEventIds);
        DELETE FROM SmartStudy_Tasks      WHERE TaskId  IN (SELECT TaskId  FROM @StudyTaskIds);
    END

    DELETE FROM SmartStudy_Exams WHERE ExamId = @ExamId;
END
GO

-- ===== TASKS =====

IF OBJECT_ID('SS_Tasks_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetByUser;
GO
CREATE PROCEDURE SS_Tasks_GetByUser
    @Email NVARCHAR(255),
    @CourseId INT = NULL,
    @Completed BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId, t.CourseId, c.CourseName, t.Title, t.[Type], t.EstimatedHours,
           t.DueDate, t.IsCompleted, t.[Priority], t.ActualHours, t.ParentTaskId,
           t.AllowSplitting, t.IsManuallyPinned, t.IsManualPriority, t.Email
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON t.CourseId = c.CourseId
    WHERE t.Email = @Email AND t.ParentTaskId IS NULL
      AND (@CourseId IS NULL OR t.CourseId = @CourseId)
      AND (@Completed IS NULL OR t.IsCompleted = @Completed)
    ORDER BY t.IsCompleted, t.DueDate;
END
GO

IF OBJECT_ID('SS_Tasks_GetSubTasks','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetSubTasks;
GO
CREATE PROCEDURE SS_Tasks_GetSubTasks
    @ParentTaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId, t.CourseId, c.CourseName, t.Title, t.[Type], t.EstimatedHours,
           t.DueDate, t.IsCompleted, t.[Priority], t.ActualHours, t.ParentTaskId,
           t.AllowSplitting, t.IsManuallyPinned, t.IsManualPriority, t.Email
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON t.CourseId = c.CourseId
    WHERE t.ParentTaskId = @ParentTaskId;
END
GO

IF OBJECT_ID('SS_Tasks_GetById','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetById;
GO
CREATE PROCEDURE SS_Tasks_GetById
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId, t.CourseId, c.CourseName, t.Title, t.[Type], t.EstimatedHours,
           t.DueDate, t.IsCompleted, t.[Priority], t.ActualHours, t.ParentTaskId,
           t.AllowSplitting, t.IsManuallyPinned, t.IsManualPriority, t.Email
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON t.CourseId = c.CourseId
    WHERE t.TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_Tasks_Create','P') IS NOT NULL DROP PROCEDURE SS_Tasks_Create;
GO
CREATE PROCEDURE SS_Tasks_Create
    @CourseId INT,
    @Email NVARCHAR(255),
    @Title NVARCHAR(200),
    @Type NVARCHAR(50),
    @EstimatedHours DECIMAL(5,2) = NULL,
    @DueDate DATETIME2 = NULL,
    @ParentTaskId INT = NULL,
    @AllowSplitting BIT = 0,
    @Priority NVARCHAR(20) = NULL,
    @IsManualPriority BIT = 0,
    @IsManuallyPinned BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Tasks (CourseId, Email, Title, [Type], EstimatedHours, DueDate, IsCompleted, ParentTaskId, AllowSplitting, [Priority], IsManualPriority, IsManuallyPinned)
    VALUES (@CourseId, @Email, @Title, @Type, @EstimatedHours, @DueDate, 0, @ParentTaskId, @AllowSplitting, @Priority, @IsManualPriority, @IsManuallyPinned);
    SELECT SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('SS_Tasks_Update','P') IS NOT NULL DROP PROCEDURE SS_Tasks_Update;
GO
CREATE PROCEDURE SS_Tasks_Update
    @TaskId INT,
    @CourseId INT = NULL,
    @Title NVARCHAR(200) = NULL,
    @Type NVARCHAR(50) = NULL,
    @EstimatedHours DECIMAL(5,2) = NULL,
    @DueDate DATETIME2 = NULL,
    @IsCompleted BIT = NULL,
    @AllowSplitting BIT = NULL,
    @IsManuallyPinned BIT = NULL,
    @Priority NVARCHAR(20) = NULL,
    @IsManualPriority BIT = NULL,
    @ActualHours DECIMAL(5,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Tasks
    SET CourseId = ISNULL(@CourseId, CourseId),
        Title = ISNULL(@Title, Title),
        [Type] = ISNULL(@Type, [Type]),
        EstimatedHours = ISNULL(@EstimatedHours, EstimatedHours),
        DueDate = ISNULL(@DueDate, DueDate),
        IsCompleted = ISNULL(@IsCompleted, IsCompleted),
        AllowSplitting = ISNULL(@AllowSplitting, AllowSplitting),
        IsManuallyPinned = ISNULL(@IsManuallyPinned, IsManuallyPinned),
        [Priority] = ISNULL(@Priority, [Priority]),
        IsManualPriority = ISNULL(@IsManualPriority, IsManualPriority),
        ActualHours = CASE WHEN @ActualHours IS NOT NULL THEN @ActualHours ELSE ActualHours END
    WHERE TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_Tasks_Delete','P') IS NOT NULL DROP PROCEDURE SS_Tasks_Delete;
GO
CREATE PROCEDURE SS_Tasks_Delete
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TasksToDelete TABLE (TaskId INT PRIMARY KEY);
    INSERT INTO @TasksToDelete (TaskId) VALUES (@TaskId);
    INSERT INTO @TasksToDelete (TaskId)
        SELECT TaskId FROM SmartStudy_Tasks
        WHERE ParentTaskId = @TaskId AND TaskId NOT IN (SELECT TaskId FROM @TasksToDelete);

    DECLARE @EventIds TABLE (EventId INT PRIMARY KEY);
    INSERT INTO @EventIds (EventId)
        SELECT te.EventId FROM SmartStudy_TaskEvents te
        WHERE te.TaskId IN (SELECT TaskId FROM @TasksToDelete);

    -- TaskEvents → Events FK is NO_ACTION on the live DB (EF-Core default name).
    -- Delete the subtype rows explicitly so the base Events deletes can succeed.
    DELETE FROM SmartStudy_TaskEvents WHERE EventId IN (SELECT EventId FROM @EventIds);
    DELETE FROM SmartStudy_Events     WHERE EventId IN (SELECT EventId FROM @EventIds);

    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = NULL
    WHERE CopyTaskId IN (SELECT TaskId FROM @TasksToDelete);

    DELETE FROM SmartStudy_Tasks WHERE ParentTaskId = @TaskId;
    DELETE FROM SmartStudy_Tasks WHERE TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_Tasks_Complete','P') IS NOT NULL DROP PROCEDURE SS_Tasks_Complete;
GO
CREATE PROCEDURE SS_Tasks_Complete
    @TaskId INT,
    @IsCompleted BIT,
    @ActualHours DECIMAL(5,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Tasks
    SET IsCompleted = @IsCompleted,
        ActualHours = CASE WHEN @IsCompleted = 1 AND @ActualHours IS NOT NULL THEN @ActualHours
                           WHEN @IsCompleted = 0 THEN NULL
                           ELSE ActualHours END
    WHERE TaskId = @TaskId;

    -- Remove task events when completing
    IF @IsCompleted = 1
    BEGIN
        DELETE FROM SmartStudy_Events WHERE EventId IN (SELECT EventId FROM SmartStudy_TaskEvents WHERE TaskId = @TaskId);
    END
END
GO

IF OBJECT_ID('SS_Tasks_GetTaskEvents','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetTaskEvents;
GO
CREATE PROCEDURE SS_Tasks_GetTaskEvents
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.EventId, e.[From], e.[To], te.[Priority], te.ActualHours, te.[Status]
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON te.EventId = e.EventId
    WHERE te.TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_Tasks_GetSharedInfo','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetSharedInfo;
GO
CREATE PROCEDURE SS_Tasks_GetSharedInfo
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Resolve: @TaskId may be the original shared task OR a partner's copy
    DECLARE @OriginalTaskId INT = @TaskId;
    IF NOT EXISTS (SELECT 1 FROM SmartStudy_SharedTasks WHERE TaskId = @TaskId)
    BEGIN
        SELECT TOP 1 @OriginalTaskId = TaskId
        FROM SmartStudy_SharedTaskMembers
        WHERE CopyTaskId = @TaskId;
    END

    SELECT st.TaskId, st.SharedStatus, st.CreatedByEmail,
           stm.Email AS MemberEmail, stm.ResponseStatus,
           u.FirstName, u.LastName
    FROM SmartStudy_SharedTasks st
    LEFT JOIN SmartStudy_SharedTaskMembers stm ON st.TaskId = stm.TaskId
    LEFT JOIN SmartStudy_Users u ON stm.Email = u.Email
    WHERE st.TaskId = @OriginalTaskId;
END
GO

IF OBJECT_ID('SS_Tasks_GetMLData','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetMLData;
GO
CREATE PROCEDURE SS_Tasks_GetMLData
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ActualHours, EstimatedHours
    FROM SmartStudy_Tasks
    WHERE Email = @Email AND CourseId = @CourseId
      AND IsCompleted = 1 AND ActualHours IS NOT NULL AND EstimatedHours IS NOT NULL AND EstimatedHours > 0;
END
GO

IF OBJECT_ID('SS_Tasks_GetMLInsights','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetMLInsights;
GO
CREATE PROCEDURE SS_Tasks_GetMLInsights
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.CourseId, c.CourseName,
           COUNT(*) AS TaskCount,
           AVG(CAST(t.EstimatedHours AS FLOAT)) AS AvgEstimated,
           AVG(CAST(t.ActualHours AS FLOAT)) AS AvgActual,
           AVG(CAST(CASE WHEN t.EstimatedHours >= t.ActualHours
               THEN CAST(t.ActualHours AS FLOAT) / CAST(t.EstimatedHours AS FLOAT)
               ELSE CAST(t.EstimatedHours AS FLOAT) / CAST(t.ActualHours AS FLOAT) END AS FLOAT)) * 100 AS Accuracy
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON t.CourseId = c.CourseId
    WHERE t.Email = @Email AND t.IsCompleted = 1
      AND t.ActualHours IS NOT NULL AND t.EstimatedHours IS NOT NULL AND t.EstimatedHours > 0
    GROUP BY t.CourseId, c.CourseName
    ORDER BY c.CourseName;
END
GO

IF OBJECT_ID('SS_Tasks_CheckAllSiblingsComplete','P') IS NOT NULL DROP PROCEDURE SS_Tasks_CheckAllSiblingsComplete;
GO
CREATE PROCEDURE SS_Tasks_CheckAllSiblingsComplete
    @ParentTaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM SmartStudy_Tasks WHERE ParentTaskId = @ParentTaskId AND IsCompleted = 0) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_SharedTasks_Create','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_Create;
GO
CREATE PROCEDURE SS_SharedTasks_Create
    @TaskId INT,
    @CreatedByEmail NVARCHAR(255),
    @SharedStatus NVARCHAR(20) = 'Pending'
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_SharedTasks (TaskId, CreatedByEmail, CreatedAt, SharedStatus)
    VALUES (@TaskId, @CreatedByEmail, GETUTCDATE(), @SharedStatus);
END
GO

IF OBJECT_ID('SS_SharedTasks_UpdateStatus','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_UpdateStatus;
GO
CREATE PROCEDURE SS_SharedTasks_UpdateStatus
    @TaskId INT,
    @SharedStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_SharedTasks SET SharedStatus = @SharedStatus WHERE TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_SharedTaskMembers_Create','P') IS NOT NULL DROP PROCEDURE SS_SharedTaskMembers_Create;
GO
CREATE PROCEDURE SS_SharedTaskMembers_Create
    @TaskId INT,
    @Email NVARCHAR(255),
    @ResponseStatus NVARCHAR(20) = 'Pending',
    @RespondedAt DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_SharedTaskMembers (TaskId, Email, ResponseStatus, RespondedAt)
    VALUES (@TaskId, @Email, @ResponseStatus, @RespondedAt);
END
GO

IF OBJECT_ID('SS_UserCourses_GetByEmailAndCourse','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_GetByEmailAndCourse;
GO
CREATE PROCEDURE SS_UserCourses_GetByEmailAndCourse
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email, CourseId, StudyPartnerEmail, SharedByDefault, CourseShareApproved
    FROM SmartStudy_UserCourses
    WHERE Email = @Email AND CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_Tasks_DeleteStudyTasksForExam','P') IS NOT NULL DROP PROCEDURE SS_Tasks_DeleteStudyTasksForExam;
GO
CREATE PROCEDURE SS_Tasks_DeleteStudyTasksForExam
    @Email NVARCHAR(255),
    @CourseId INT,
    @ExamDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StudyTaskIds TABLE (TaskId INT PRIMARY KEY);
    INSERT INTO @StudyTaskIds
    SELECT TaskId FROM SmartStudy_Tasks
    WHERE Email = @Email AND CourseId = @CourseId
      AND [Type] = 'Study for exam' AND CAST(DueDate AS DATE) = CAST(@ExamDate AS DATE)
      AND IsCompleted = 0;

    DECLARE @StudyEventIds TABLE (EventId INT PRIMARY KEY);
    INSERT INTO @StudyEventIds
    SELECT te.EventId FROM SmartStudy_TaskEvents te
    WHERE te.TaskId IN (SELECT TaskId FROM @StudyTaskIds);

    DELETE FROM SmartStudy_TaskEvents WHERE EventId IN (SELECT EventId FROM @StudyEventIds);
    DELETE FROM SmartStudy_Events     WHERE EventId IN (SELECT EventId FROM @StudyEventIds);
    DELETE FROM SmartStudy_Tasks      WHERE TaskId  IN (SELECT TaskId  FROM @StudyTaskIds);
END
GO

-- ===== FRIEND REQUESTS =====

IF OBJECT_ID('SS_FriendRequests_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_FriendRequests_GetByUser;
GO
CREATE PROCEDURE SS_FriendRequests_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    -- Returns pending incoming, sent pending, and completed requests
    SELECT r.RequestId, r.RequesterEmail, r.AddresseeEmail, r.[Status], r.RequestedAt, r.RespondedAt,
           u.FirstName, u.LastName,
           CASE WHEN r.AddresseeEmail = @Email THEN r.RequesterEmail ELSE r.AddresseeEmail END AS FriendEmail
    FROM SmartStudy_FriendRequests r
    INNER JOIN SmartStudy_Users u
        ON u.Email = CASE WHEN r.AddresseeEmail = @Email THEN r.RequesterEmail ELSE r.AddresseeEmail END
    WHERE (r.AddresseeEmail = @Email OR r.RequesterEmail = @Email) AND r.[Status] = 'Pending'
    ORDER BY r.RequestedAt DESC;
END
GO

IF OBJECT_ID('SS_FriendRequests_Create','P') IS NOT NULL DROP PROCEDURE SS_FriendRequests_Create;
GO
CREATE PROCEDURE SS_FriendRequests_Create
    @RequesterEmail NVARCHAR(255),
    @AddresseeEmail NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    -- Self-check
    IF @RequesterEmail = @AddresseeEmail
    BEGIN
        RAISERROR('Cannot invite yourself', 16, 1);
        RETURN;
    END

    -- Target user must exist
    IF NOT EXISTS (SELECT 1 FROM SmartStudy_Users WHERE Email = @AddresseeEmail)
    BEGIN
        RAISERROR('User not found', 16, 2);
        RETURN;
    END

    -- Check duplicate pending request (either direction)
    IF EXISTS (SELECT 1 FROM SmartStudy_FriendRequests
        WHERE [Status] = 'Pending'
          AND ((RequesterEmail = @RequesterEmail AND AddresseeEmail = @AddresseeEmail)
            OR (RequesterEmail = @AddresseeEmail AND AddresseeEmail = @RequesterEmail)))
    BEGIN
        RAISERROR('A pending request already exists', 16, 3);
        RETURN;
    END

    -- Check existing active friendship
    DECLARE @E1 NVARCHAR(255), @E2 NVARCHAR(255);
    IF @RequesterEmail < @AddresseeEmail
    BEGIN SET @E1 = @RequesterEmail; SET @E2 = @AddresseeEmail; END
    ELSE
    BEGIN SET @E1 = @AddresseeEmail; SET @E2 = @RequesterEmail; END

    IF EXISTS (SELECT 1 FROM SmartStudy_Friendships WHERE Email1 = @E1 AND Email2 = @E2 AND IsActive = 1)
    BEGIN
        RAISERROR('Already friends', 16, 4);
        RETURN;
    END

    INSERT INTO SmartStudy_FriendRequests (RequesterEmail, AddresseeEmail, [Status], RequestedAt)
    VALUES (@RequesterEmail, @AddresseeEmail, 'Pending', GETDATE());

    SELECT SCOPE_IDENTITY() AS RequestId;
END
GO


IF OBJECT_ID('SS_FriendRequests_UpdateStatus','P') IS NOT NULL DROP PROCEDURE SS_FriendRequests_UpdateStatus;
GO
CREATE PROCEDURE SS_FriendRequests_UpdateStatus
    @RequestId INT,
    @AddresseeEmail NVARCHAR(255),
    @NewStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_FriendRequests
    SET [Status] = @NewStatus, RespondedAt = GETDATE()
    WHERE RequestId = @RequestId AND AddresseeEmail = @AddresseeEmail AND [Status] = 'Pending';

    -- Return the updated request with requester info
    SELECT r.RequestId, r.RequesterEmail, r.AddresseeEmail, r.[Status], r.RequestedAt, r.RespondedAt
    FROM SmartStudy_FriendRequests r
    WHERE r.RequestId = @RequestId;
END
GO

-- ===== FRIENDSHIPS =====

IF OBJECT_ID('SS_Friendships_ExistsPair','P') IS NOT NULL DROP PROCEDURE SS_Friendships_ExistsPair;
GO
CREATE PROCEDURE SS_Friendships_ExistsPair
    @Email1 NVARCHAR(255),
    @Email2 NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM SmartStudy_Friendships WHERE Email1 = @Email1 AND Email2 = @Email2 AND IsActive = 1) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_Friendships_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Friendships_GetByUser;
GO
CREATE PROCEDURE SS_Friendships_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT f.FriendshipId, f.Email1, f.Email2, f.CreatedAt, f.IsActive,
           u.Email AS FriendEmail, u.FirstName, u.LastName
    FROM SmartStudy_Friendships f
    INNER JOIN SmartStudy_Users u
        ON u.Email = CASE WHEN f.Email1 = @Email THEN f.Email2 ELSE f.Email1 END
    WHERE (f.Email1 = @Email OR f.Email2 = @Email) AND f.IsActive = 1
    ORDER BY f.CreatedAt DESC;
END
GO

IF OBJECT_ID('SS_Friendships_Create','P') IS NOT NULL DROP PROCEDURE SS_Friendships_Create;
GO
CREATE PROCEDURE SS_Friendships_Create
    @Email1 NVARCHAR(255),
    @Email2 NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    -- Normalize the pair
    DECLARE @E1 NVARCHAR(255), @E2 NVARCHAR(255);
    IF @Email1 < @Email2
    BEGIN SET @E1 = @Email1; SET @E2 = @Email2; END
    ELSE
    BEGIN SET @E1 = @Email2; SET @E2 = @Email1; END

    INSERT INTO SmartStudy_Friendships (Email1, Email2, CreatedAt, IsActive)
    VALUES (@E1, @E2, GETDATE(), 1);

    SELECT SCOPE_IDENTITY() AS FriendshipId;
END
GO

IF OBJECT_ID('SS_Friendships_Deactivate','P') IS NOT NULL DROP PROCEDURE SS_Friendships_Deactivate;
GO
CREATE PROCEDURE SS_Friendships_Deactivate
    @FriendshipId INT,
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Friendships
    SET IsActive = 0
    WHERE FriendshipId = @FriendshipId
      AND (Email1 = @Email OR Email2 = @Email)
      AND IsActive = 1;
    SELECT @@ROWCOUNT AS AffectedRows;
END
GO


-- ===== SHARED TASKS (full CRUD) =====

IF OBJECT_ID('SS_SharedTasks_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_GetByUser;
GO
CREATE PROCEDURE SS_SharedTasks_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    -- Return shared tasks + task info + creator info + all members
    SELECT st.TaskId, t.Title AS TaskTitle, t.CourseId, c.CourseName,
           st.CreatedByEmail, cu.FirstName AS CreatorFirstName, cu.LastName AS CreatorLastName,
           st.CreatedAt, st.SharedStatus,
           m.Email AS MemberEmail, mu.FirstName AS MemberFirstName, mu.LastName AS MemberLastName,
           m.ResponseStatus, m.RespondedAt
    FROM SmartStudy_SharedTaskMembers mem
    INNER JOIN SmartStudy_SharedTasks st ON st.TaskId = mem.TaskId
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = st.TaskId
    LEFT JOIN SmartStudy_Courses c ON c.CourseId = t.CourseId
    INNER JOIN SmartStudy_Users cu ON cu.Email = st.CreatedByEmail
    INNER JOIN SmartStudy_SharedTaskMembers m ON m.TaskId = st.TaskId
    INNER JOIN SmartStudy_Users mu ON mu.Email = m.Email
    WHERE mem.Email = @Email
    ORDER BY st.TaskId, m.Email;
END
GO

IF OBJECT_ID('SS_SharedTasks_GetByTaskId','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_GetByTaskId;
GO
CREATE PROCEDURE SS_SharedTasks_GetByTaskId
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.TaskId, t.Title AS TaskTitle, t.CourseId, c.CourseName,
           st.CreatedByEmail, cu.FirstName AS CreatorFirstName, cu.LastName AS CreatorLastName,
           st.CreatedAt, st.SharedStatus,
           m.Email AS MemberEmail, mu.FirstName AS MemberFirstName, mu.LastName AS MemberLastName,
           m.ResponseStatus, m.RespondedAt
    FROM SmartStudy_SharedTasks st
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = st.TaskId
    LEFT JOIN SmartStudy_Courses c ON c.CourseId = t.CourseId
    INNER JOIN SmartStudy_Users cu ON cu.Email = st.CreatedByEmail
    INNER JOIN SmartStudy_SharedTaskMembers m ON m.TaskId = st.TaskId
    INNER JOIN SmartStudy_Users mu ON mu.Email = m.Email
    WHERE st.TaskId = @TaskId
    ORDER BY m.Email;
END
GO

IF OBJECT_ID('SS_SharedTasks_ExistsByTaskId','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_ExistsByTaskId;
GO
CREATE PROCEDURE SS_SharedTasks_ExistsByTaskId
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM SmartStudy_SharedTasks WHERE TaskId = @TaskId) THEN 1 ELSE 0 END;
END
GO


IF OBJECT_ID('SS_SharedTaskMembers_UpdateStatus','P') IS NOT NULL DROP PROCEDURE SS_SharedTaskMembers_UpdateStatus;
GO
CREATE PROCEDURE SS_SharedTaskMembers_UpdateStatus
    @TaskId INT,
    @Email NVARCHAR(255),
    @ResponseStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_SharedTaskMembers
    SET ResponseStatus = @ResponseStatus, RespondedAt = GETDATE()
    WHERE TaskId = @TaskId AND Email = @Email AND ResponseStatus = 'Pending';
    SELECT @@ROWCOUNT AS AffectedRows;
END
GO

IF OBJECT_ID('SS_SharedTaskMembers_AllAccepted','P') IS NOT NULL DROP PROCEDURE SS_SharedTaskMembers_AllAccepted;
GO
CREATE PROCEDURE SS_SharedTaskMembers_AllAccepted
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN NOT EXISTS (
        SELECT 1 FROM SmartStudy_SharedTaskMembers WHERE TaskId = @TaskId AND ResponseStatus <> 'Accepted'
    ) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_SharedTaskMembers_GetEmails','P') IS NOT NULL DROP PROCEDURE SS_SharedTaskMembers_GetEmails;
GO
CREATE PROCEDURE SS_SharedTaskMembers_GetEmails
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email FROM SmartStudy_SharedTaskMembers WHERE TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_SharedTaskMembers_GetCopyTaskId','P') IS NOT NULL DROP PROCEDURE SS_SharedTaskMembers_GetCopyTaskId;
GO
CREATE PROCEDURE SS_SharedTaskMembers_GetCopyTaskId
    @TaskId INT,
    @Email  NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 CopyTaskId
    FROM SmartStudy_SharedTaskMembers
    WHERE TaskId = @TaskId AND Email = @Email;
END
GO

IF OBJECT_ID('SS_SharedTasks_CleanupPartnerCopies','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_CleanupPartnerCopies;
GO
CREATE PROCEDURE SS_SharedTasks_CleanupPartnerCopies
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CopyIds TABLE (TaskId INT PRIMARY KEY);
    INSERT INTO @CopyIds (TaskId)
    SELECT DISTINCT stm.CopyTaskId
    FROM SmartStudy_SharedTaskMembers stm
    INNER JOIN SmartStudy_SharedTasks st ON st.TaskId = stm.TaskId
    WHERE stm.TaskId = @TaskId
      AND stm.CopyTaskId IS NOT NULL
      AND stm.Email <> st.CreatedByEmail;

    DECLARE @CopyEventIds TABLE (EventId INT PRIMARY KEY);
    INSERT INTO @CopyEventIds
    SELECT te.EventId FROM SmartStudy_TaskEvents te
    WHERE te.TaskId IN (SELECT TaskId FROM @CopyIds);

    DELETE FROM SmartStudy_TaskEvents WHERE EventId IN (SELECT EventId FROM @CopyEventIds);
    DELETE FROM SmartStudy_Events     WHERE EventId IN (SELECT EventId FROM @CopyEventIds);
    DELETE FROM SmartStudy_Tasks      WHERE TaskId  IN (SELECT TaskId  FROM @CopyIds);

    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = NULL
    WHERE TaskId = @TaskId;

    SELECT COUNT(*) AS DeletedCount FROM @CopyIds;
END
GO

IF OBJECT_ID('SS_SharedTaskMembers_UpdateCopyTaskId','P') IS NOT NULL DROP PROCEDURE SS_SharedTaskMembers_UpdateCopyTaskId;
GO
CREATE PROCEDURE SS_SharedTaskMembers_UpdateCopyTaskId
    @TaskId INT,
    @Email NVARCHAR(255),
    @CopyTaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = @CopyTaskId
    WHERE TaskId = @TaskId AND Email = @Email;
END
GO

-- ===== COLLABORATION =====

IF OBJECT_ID('SS_Collaboration_GetFriendshipForUser','P') IS NOT NULL DROP PROCEDURE SS_Collaboration_GetFriendshipForUser;
GO
CREATE PROCEDURE SS_Collaboration_GetFriendshipForUser
    @FriendshipId INT,
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT f.FriendshipId, f.Email1, f.Email2, f.CreatedAt, f.IsActive
    FROM SmartStudy_Friendships f
    WHERE f.FriendshipId = @FriendshipId
      AND (f.Email1 = @Email OR f.Email2 = @Email)
      AND f.IsActive = 1;
END
GO

IF OBJECT_ID('SS_UserCourses_SetCourseShareApproved','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_SetCourseShareApproved;
GO
CREATE PROCEDURE SS_UserCourses_SetCourseShareApproved
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_UserCourses
    SET CourseShareApproved = 1
    WHERE Email = @Email AND CourseId = @CourseId;
    SELECT @@ROWCOUNT AS AffectedRows;
END
GO

IF OBJECT_ID('SS_Collaboration_GetPendingMembersForCourse','P') IS NOT NULL DROP PROCEDURE SS_Collaboration_GetPendingMembersForCourse;
GO
CREATE PROCEDURE SS_Collaboration_GetPendingMembersForCourse
    @Email NVARCHAR(255),
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.TaskId, m.Email, m.ResponseStatus, m.RespondedAt,
           st.CreatedByEmail, st.SharedStatus
    FROM SmartStudy_SharedTaskMembers m
    INNER JOIN SmartStudy_SharedTasks st ON st.TaskId = m.TaskId
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = st.TaskId
    WHERE m.Email = @Email AND m.ResponseStatus = 'Pending' AND t.CourseId = @CourseId;
END
GO

-- ===== NOTIFICATIONS =====

IF OBJECT_ID('SS_Notifications_Create','P') IS NOT NULL DROP PROCEDURE SS_Notifications_Create;
GO
CREATE PROCEDURE SS_Notifications_Create
    @Email NVARCHAR(255),
    @Type NVARCHAR(50),
    @Title NVARCHAR(200),
    @Message NVARCHAR(1000),
    @RelatedEntityId INT = NULL,
    @RelatedEntityType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Notifications (Email, [Type], Title, [Message], IsRead, CreatedAt, RelatedEntityId, RelatedEntityType)
    VALUES (@Email, @Type, @Title, @Message, 0, GETDATE(), @RelatedEntityId, @RelatedEntityType);
    SELECT SCOPE_IDENTITY();
END
GO

-- ===== WORK EVENTS =====

IF OBJECT_ID('SS_WorkEvents_Create','P') IS NOT NULL DROP PROCEDURE SS_WorkEvents_Create;
GO
CREATE PROCEDURE SS_WorkEvents_Create
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @WorkPlace NVARCHAR(200) = NULL,
    @TravelTime INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Events (Email, [From], [To], Recurring, RecurrenceEndDate)
    VALUES (@Email, @From, @To, @Recurring, @RecurrenceEndDate);

    DECLARE @EventId INT = SCOPE_IDENTITY();

    INSERT INTO SmartStudy_WorkEvents (EventId, TravelTime, WorkPlace)
    VALUES (@EventId, @TravelTime, @WorkPlace);

    SELECT @EventId;
END
GO

-- ===== PERSONAL EVENTS =====

IF OBJECT_ID('SS_PersonalEvents_Create','P') IS NOT NULL DROP PROCEDURE SS_PersonalEvents_Create;
GO
CREATE PROCEDURE SS_PersonalEvents_Create
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @Type NVARCHAR(50) = NULL,
    @Description NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Events (Email, [From], [To], Recurring, RecurrenceEndDate)
    VALUES (@Email, @From, @To, @Recurring, @RecurrenceEndDate);

    DECLARE @EventId INT = SCOPE_IDENTITY();

    INSERT INTO SmartStudy_PersonalEvents (EventId, [Type], [Description])
    VALUES (@EventId, @Type, @Description);

    SELECT @EventId;
END
GO

-- ===== EVENTS (Full CRUD - Phase 5) =====

IF OBJECT_ID('SS_Events_GetAllTypedInRange','P') IS NOT NULL DROP PROCEDURE SS_Events_GetAllTypedInRange;
GO
CREATE PROCEDURE SS_Events_GetAllTypedInRange
    @Email NVARCHAR(255),
    @From DATETIME2 = NULL,
    @To DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.EventId,
        e.Email,
        e.[From],
        e.[To],
        e.Recurring,
        e.RecurrenceEndDate,
        CASE
            WHEN ce.EventId IS NOT NULL THEN 'class'
            WHEN te.EventId IS NOT NULL THEN 'task'
            WHEN we.EventId IS NOT NULL THEN 'work'
            WHEN pe.EventId IS NOT NULL THEN 'personal'
            ELSE 'unknown'
        END AS EventType,
        -- ClassEvent fields
        ce.CourseId,
        c.CourseName,
        ce.[Location],
        ce.Duration,
        -- TaskEvent fields
        te.TaskId,
        t.Title AS TaskTitle,
        te.[Priority],
        te.ActualHours,
        te.[Status],
        t.IsManuallyPinned,
        -- Shared indicator: task is either the creator's original or a partner's copy of a non-cancelled SharedTask
        CAST(CASE
            WHEN st_direct.TaskId IS NOT NULL OR st_copy.TaskId IS NOT NULL THEN 1
            ELSE 0
        END AS BIT) AS IsShared,
        COALESCE(st_direct.SharedStatus, st_copy.SharedStatus) AS SharedStatus,
        -- WorkEvent fields
        we.TravelTime,
        we.WorkPlace,
        -- PersonalEvent fields
        pe.[Type],
        pe.[Description]
    FROM SmartStudy_Events e
    LEFT JOIN SmartStudy_ClassEvents ce ON ce.EventId = e.EventId
    LEFT JOIN SmartStudy_Courses c ON c.CourseId = ce.CourseId
    LEFT JOIN SmartStudy_TaskEvents te ON te.EventId = e.EventId
    LEFT JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    LEFT JOIN SmartStudy_SharedTasks st_direct ON st_direct.TaskId = te.TaskId AND st_direct.SharedStatus <> 'Cancelled'
    LEFT JOIN SmartStudy_SharedTaskMembers stm_copy ON stm_copy.CopyTaskId = te.TaskId
    LEFT JOIN SmartStudy_SharedTasks st_copy ON st_copy.TaskId = stm_copy.TaskId AND st_copy.SharedStatus <> 'Cancelled'
    LEFT JOIN SmartStudy_WorkEvents we ON we.EventId = e.EventId
    LEFT JOIN SmartStudy_PersonalEvents pe ON pe.EventId = e.EventId
    WHERE e.Email = @Email
      AND (
        (@From IS NULL AND @To IS NULL)
        OR (@From IS NOT NULL AND @To IS NOT NULL AND ((e.[To] >= @From AND e.[From] <= @To) OR e.Recurring = 1))
        OR (@From IS NOT NULL AND @To IS NULL AND e.[To] >= @From)
        OR (@From IS NULL AND @To IS NOT NULL AND e.[From] <= @To)
      )
    ORDER BY e.[From];
END
GO

IF OBJECT_ID('SS_ClassEvents_Create','P') IS NOT NULL DROP PROCEDURE SS_ClassEvents_Create;
GO
CREATE PROCEDURE SS_ClassEvents_Create
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @CourseId INT,
    @Location NVARCHAR(200) = NULL,
    @Duration DECIMAL(5,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Events (Email, [From], [To], Recurring, RecurrenceEndDate)
    VALUES (@Email, @From, @To, @Recurring, @RecurrenceEndDate);

    DECLARE @EventId INT = SCOPE_IDENTITY();

    INSERT INTO SmartStudy_ClassEvents (EventId, CourseId, [Location], Duration)
    VALUES (@EventId, @CourseId, @Location, @Duration);

    SELECT @EventId;
END
GO

IF OBJECT_ID('SS_ClassEvents_Update','P') IS NOT NULL DROP PROCEDURE SS_ClassEvents_Update;
GO
CREATE PROCEDURE SS_ClassEvents_Update
    @EventId INT,
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @CourseId INT,
    @Location NVARCHAR(200) = NULL,
    @Duration DECIMAL(5,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Events
    SET [From] = @From, [To] = @To, Recurring = @Recurring, RecurrenceEndDate = @RecurrenceEndDate
    WHERE EventId = @EventId;

    UPDATE SmartStudy_ClassEvents
    SET CourseId = @CourseId, [Location] = @Location, Duration = @Duration
    WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_TaskEvents_Create','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_Create;
GO
CREATE PROCEDURE SS_TaskEvents_Create
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @TaskId INT,
    @Priority NVARCHAR(20) = NULL,
    @Status NVARCHAR(50) = 'Scheduled'
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Events (Email, [From], [To], Recurring, RecurrenceEndDate)
    VALUES (@Email, @From, @To, @Recurring, @RecurrenceEndDate);

    DECLARE @EventId INT = SCOPE_IDENTITY();

    INSERT INTO SmartStudy_TaskEvents (EventId, TaskId, [Priority], [Status])
    VALUES (@EventId, @TaskId, @Priority, @Status);

    SELECT @EventId;
END
GO

IF OBJECT_ID('SS_TaskEvents_Update','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_Update;
GO
CREATE PROCEDURE SS_TaskEvents_Update
    @EventId INT,
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @TaskId INT,
    @Priority NVARCHAR(20) = NULL,
    @Status NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Events
    SET [From] = @From, [To] = @To, Recurring = @Recurring, RecurrenceEndDate = @RecurrenceEndDate
    WHERE EventId = @EventId;

    UPDATE SmartStudy_TaskEvents
    SET TaskId = @TaskId,
        [Priority] = ISNULL(@Priority, [Priority]),
        [Status] = ISNULL(@Status, [Status])
    WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_WorkEvents_Update','P') IS NOT NULL DROP PROCEDURE SS_WorkEvents_Update;
GO
CREATE PROCEDURE SS_WorkEvents_Update
    @EventId INT,
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @WorkPlace NVARCHAR(200) = NULL,
    @TravelTime INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Events
    SET [From] = @From, [To] = @To, Recurring = @Recurring, RecurrenceEndDate = @RecurrenceEndDate
    WHERE EventId = @EventId;

    UPDATE SmartStudy_WorkEvents
    SET WorkPlace = @WorkPlace, TravelTime = @TravelTime
    WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_PersonalEvents_Update','P') IS NOT NULL DROP PROCEDURE SS_PersonalEvents_Update;
GO
CREATE PROCEDURE SS_PersonalEvents_Update
    @EventId INT,
    @From DATETIME2,
    @To DATETIME2,
    @Recurring BIT = 0,
    @RecurrenceEndDate DATETIME2 = NULL,
    @Type NVARCHAR(50) = NULL,
    @Description NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Events
    SET [From] = @From, [To] = @To, Recurring = @Recurring, RecurrenceEndDate = @RecurrenceEndDate
    WHERE EventId = @EventId;

    UPDATE SmartStudy_PersonalEvents
    SET [Type] = @Type, [Description] = @Description
    WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_Events_Delete','P') IS NOT NULL DROP PROCEDURE SS_Events_Delete;
GO
CREATE PROCEDURE SS_Events_Delete
    @EventId INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Subtype rows cascade-delete via FK
    DELETE FROM SmartStudy_Events WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_Events_ChangeType','P') IS NOT NULL DROP PROCEDURE SS_Events_ChangeType;
GO
CREATE PROCEDURE SS_Events_ChangeType
    @EventId INT,
    @OldType NVARCHAR(20),
    @NewType NVARCHAR(20),
    -- Work fields
    @WorkPlace NVARCHAR(200) = NULL,
    @TravelTime INT = NULL,
    -- Personal fields
    @Type NVARCHAR(50) = NULL,
    @Description NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    -- Delete from old subtype table
    IF @OldType = 'work'
        DELETE FROM SmartStudy_WorkEvents WHERE EventId = @EventId;
    ELSE IF @OldType = 'personal'
        DELETE FROM SmartStudy_PersonalEvents WHERE EventId = @EventId;

    -- Insert into new subtype table
    IF @NewType = 'work'
        INSERT INTO SmartStudy_WorkEvents (EventId, WorkPlace, TravelTime)
        VALUES (@EventId, @WorkPlace, @TravelTime);
    ELSE IF @NewType = 'personal'
        INSERT INTO SmartStudy_PersonalEvents (EventId, [Type], [Description])
        VALUES (@EventId, @Type, @Description);

    COMMIT TRANSACTION;
END
GO

IF OBJECT_ID('SS_Events_CheckConflicts','P') IS NOT NULL DROP PROCEDURE SS_Events_CheckConflicts;
GO
CREATE PROCEDURE SS_Events_CheckConflicts
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @ExcludeEventId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- Returns all events that overlap with the given time range (including recurring ones)
    -- Recurring expansion is handled in C# - this returns the raw events
    SELECT
        e.EventId,
        e.Email,
        e.[From],
        e.[To],
        e.Recurring,
        e.RecurrenceEndDate,
        CASE
            WHEN ce.EventId IS NOT NULL THEN 'class'
            WHEN te.EventId IS NOT NULL THEN 'task'
            WHEN we.EventId IS NOT NULL THEN 'work'
            WHEN pe.EventId IS NOT NULL THEN 'personal'
            ELSE 'unknown'
        END AS EventType,
        ce.CourseId, c.CourseName, ce.[Location], ce.Duration,
        te.TaskId, t.Title AS TaskTitle, te.[Priority], te.ActualHours, te.[Status], t.IsManuallyPinned,
        CAST(CASE
            WHEN st_direct.TaskId IS NOT NULL OR st_copy.TaskId IS NOT NULL THEN 1
            ELSE 0
        END AS BIT) AS IsShared,
        we.TravelTime, we.WorkPlace,
        pe.[Type], pe.[Description]
    FROM SmartStudy_Events e
    LEFT JOIN SmartStudy_ClassEvents ce ON ce.EventId = e.EventId
    LEFT JOIN SmartStudy_Courses c ON c.CourseId = ce.CourseId
    LEFT JOIN SmartStudy_TaskEvents te ON te.EventId = e.EventId
    LEFT JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    LEFT JOIN SmartStudy_SharedTasks st_direct ON st_direct.TaskId = te.TaskId AND st_direct.SharedStatus <> 'Cancelled'
    LEFT JOIN SmartStudy_SharedTaskMembers stm_copy ON stm_copy.CopyTaskId = te.TaskId
    LEFT JOIN SmartStudy_SharedTasks st_copy ON st_copy.TaskId = stm_copy.TaskId AND st_copy.SharedStatus <> 'Cancelled'
    LEFT JOIN SmartStudy_WorkEvents we ON we.EventId = e.EventId
    LEFT JOIN SmartStudy_PersonalEvents pe ON pe.EventId = e.EventId
    WHERE e.Email = @Email
      AND ((@ExcludeEventId IS NULL) OR (e.EventId != @ExcludeEventId))
      AND ((e.[To] > @From AND e.[From] < @To) OR e.Recurring = 1)
    ORDER BY e.[From];
END
GO

IF OBJECT_ID('SS_Events_CountConflictingTaskEvents','P') IS NOT NULL DROP PROCEDURE SS_Events_CountConflictingTaskEvents;
GO
CREATE PROCEDURE SS_Events_CountConflictingTaskEvents
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @ExcludeEventId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*)
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    WHERE e.Email = @Email
      AND e.EventId != @ExcludeEventId
      AND e.[From] < @To
      AND e.[To] > @From;
END
GO

IF OBJECT_ID('SS_Events_GetOwnerEmail','P') IS NOT NULL DROP PROCEDURE SS_Events_GetOwnerEmail;
GO
CREATE PROCEDURE SS_Events_GetOwnerEmail
    @EventId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email FROM SmartStudy_Events WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_Events_GetSubtype','P') IS NOT NULL DROP PROCEDURE SS_Events_GetSubtype;
GO
CREATE PROCEDURE SS_Events_GetSubtype
    @EventId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CASE
            WHEN EXISTS (SELECT 1 FROM SmartStudy_ClassEvents WHERE EventId = @EventId) THEN 'class'
            WHEN EXISTS (SELECT 1 FROM SmartStudy_TaskEvents WHERE EventId = @EventId) THEN 'task'
            WHEN EXISTS (SELECT 1 FROM SmartStudy_WorkEvents WHERE EventId = @EventId) THEN 'work'
            WHEN EXISTS (SELECT 1 FROM SmartStudy_PersonalEvents WHERE EventId = @EventId) THEN 'personal'
            ELSE 'unknown'
        END AS EventType;
END
GO

IF OBJECT_ID('SS_Tasks_PinTask','P') IS NOT NULL DROP PROCEDURE SS_Tasks_PinTask;
GO
CREATE PROCEDURE SS_Tasks_PinTask
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Tasks SET IsManuallyPinned = 1 WHERE TaskId = @TaskId AND IsManuallyPinned = 0;
END
GO

-- ===== NOTIFICATIONS (Phase 7 - full CRUD) =====

IF OBJECT_ID('SS_Notifications_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Notifications_GetByUser;
GO
CREATE PROCEDURE SS_Notifications_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    -- Result set 1: last 50 notifications
    SELECT TOP 50 NotificationId, Email, [Type], Title, [Message], IsRead, CreatedAt, RelatedEntityId, RelatedEntityType
    FROM SmartStudy_Notifications
    WHERE Email = @Email
    ORDER BY CreatedAt DESC;

    -- Result set 2: unread count
    SELECT COUNT(*) AS UnreadCount
    FROM SmartStudy_Notifications
    WHERE Email = @Email AND IsRead = 0;
END
GO

IF OBJECT_ID('SS_Notifications_GetUnreadCount','P') IS NOT NULL DROP PROCEDURE SS_Notifications_GetUnreadCount;
GO
CREATE PROCEDURE SS_Notifications_GetUnreadCount
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM SmartStudy_Notifications WHERE Email = @Email AND IsRead = 0;
END
GO

IF OBJECT_ID('SS_Notifications_MarkRead','P') IS NOT NULL DROP PROCEDURE SS_Notifications_MarkRead;
GO
CREATE PROCEDURE SS_Notifications_MarkRead
    @Email NVARCHAR(255),
    @NotificationIds NVARCHAR(MAX)  -- comma-separated IDs
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Notifications
    SET IsRead = 1
    WHERE Email = @Email
      AND NotificationId IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@NotificationIds, ','));
END
GO

IF OBJECT_ID('SS_Notifications_MarkAllRead','P') IS NOT NULL DROP PROCEDURE SS_Notifications_MarkAllRead;
GO
CREATE PROCEDURE SS_Notifications_MarkAllRead
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Notifications SET IsRead = 1 WHERE Email = @Email AND IsRead = 0;
END
GO

IF OBJECT_ID('SS_Notifications_IsDuplicate','P') IS NOT NULL DROP PROCEDURE SS_Notifications_IsDuplicate;
GO
CREATE PROCEDURE SS_Notifications_IsDuplicate
    @Email NVARCHAR(255),
    @Type NVARCHAR(50),
    @RelatedEntityId INT = NULL,
    @RelatedEntityType NVARCHAR(50) = NULL,
    @SinceHours INT = 24
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Since DATETIME2 = DATEADD(HOUR, -@SinceHours, GETDATE());
    IF EXISTS (
        SELECT 1 FROM SmartStudy_Notifications
        WHERE Email = @Email AND [Type] = @Type AND CreatedAt > @Since
          AND ((@RelatedEntityId IS NULL AND RelatedEntityId IS NULL) OR RelatedEntityId = @RelatedEntityId)
          AND ((@RelatedEntityType IS NULL AND RelatedEntityType IS NULL) OR RelatedEntityType = @RelatedEntityType)
    )
        SELECT 1;
    ELSE
        SELECT 0;
END
GO

IF OBJECT_ID('SS_Notifications_GetUpcomingDeadlineTasks','P') IS NOT NULL DROP PROCEDURE SS_Notifications_GetUpcomingDeadlineTasks;
GO
CREATE PROCEDURE SS_Notifications_GetUpcomingDeadlineTasks
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME2 = GETDATE();
    DECLARE @In24h DATETIME2 = DATEADD(HOUR, 24, @Now);

    SELECT t.TaskId, t.Title, c.CourseName
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON t.CourseId = c.CourseId
    WHERE t.Email = @Email
      AND t.IsCompleted = 0
      AND t.DueDate IS NOT NULL
      AND t.DueDate > @Now
      AND t.DueDate <= @In24h;
END
GO

IF OBJECT_ID('SS_Notifications_GetDailySummaryData','P') IS NOT NULL DROP PROCEDURE SS_Notifications_GetDailySummaryData;
GO
CREATE PROCEDURE SS_Notifications_GetDailySummaryData
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TomorrowEnd DATETIME2 = DATEADD(DAY, 2, CAST(GETDATE() AS DATE));

    SELECT TOP 5 t.TaskId, t.Title, c.CourseName
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON t.CourseId = c.CourseId
    WHERE t.Email = @Email
      AND t.IsCompleted = 0
      AND t.DueDate IS NOT NULL
      AND t.DueDate <= @TomorrowEnd
    ORDER BY t.DueDate;
END
GO

IF OBJECT_ID('SS_Notifications_GetWeeklyPlanData','P') IS NOT NULL DROP PROCEDURE SS_Notifications_GetWeeklyPlanData;
GO
CREATE PROCEDURE SS_Notifications_GetWeeklyPlanData
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATE = CAST(GETDATE() AS DATE);
    DECLARE @WeekEnd DATE = DATEADD(DAY, 7, @Now);

    -- Result set 1: task count due this week
    SELECT COUNT(*) AS TaskCount
    FROM SmartStudy_Tasks
    WHERE Email = @Email AND IsCompleted = 0
      AND DueDate IS NOT NULL AND DueDate <= @WeekEnd;

    -- Result set 2: exam count this week
    SELECT COUNT(*) AS ExamCount
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_UserCourses uc ON e.CourseId = uc.CourseId
    WHERE uc.Email = @Email
      AND e.[Date] >= @Now AND e.[Date] <= @WeekEnd
      AND e.IsTakingExam = 1;
END
GO

IF OBJECT_ID('SS_Notifications_HasRecentWeekly','P') IS NOT NULL DROP PROCEDURE SS_Notifications_HasRecentWeekly;
GO
CREATE PROCEDURE SS_Notifications_HasRecentWeekly
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SixDaysAgo DATETIME2 = DATEADD(DAY, -6, GETDATE());
    IF EXISTS (
        SELECT 1 FROM SmartStudy_Notifications
        WHERE Email = @Email AND [Type] = 'weekly_reminder' AND CreatedAt > @SixDaysAgo
    )
        SELECT 1;
    ELSE
        SELECT 0;
END
GO

-- =====================================================
-- SCHEDULING / STRESS / SERVICES SUPPORT SPs
-- =====================================================

IF OBJECT_ID('SS_Events_GetBaseInRangeOrRecurring','P') IS NOT NULL DROP PROCEDURE SS_Events_GetBaseInRangeOrRecurring;
GO
CREATE PROCEDURE SS_Events_GetBaseInRangeOrRecurring
    @Email NVARCHAR(255),
    @RangeStart DATETIME2,
    @RangeEnd DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT EventId, Email, [From], [To], Recurring, RecurrenceEndDate
    FROM SmartStudy_Events
    WHERE Email = @Email
      AND (([From] < @RangeEnd AND [To] > @RangeStart) OR Recurring = 1)
    ORDER BY [From];
END
GO

IF OBJECT_ID('SS_Events_GetInDateRange','P') IS NOT NULL DROP PROCEDURE SS_Events_GetInDateRange;
GO
CREATE PROCEDURE SS_Events_GetInDateRange
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT EventId, Email, [From], [To], Recurring, RecurrenceEndDate
    FROM SmartStudy_Events
    WHERE Email = @Email AND [From] >= @From AND [From] < @To
    ORDER BY [From];
END
GO

IF OBJECT_ID('SS_Tasks_GetIncompleteLeaf','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetIncompleteLeaf;
GO
CREATE PROCEDURE SS_Tasks_GetIncompleteLeaf
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId, t.CourseId, c.CourseName, t.Title, t.Type, t.EstimatedHours,
           t.DueDate, t.IsCompleted, t.Priority, t.ActualHours, t.ParentTaskId,
           t.AllowSplitting, t.IsManuallyPinned, t.IsManualPriority, t.Email,
           c.Credits AS CourseCredits, c.DefaultTaskEstimatedHours,
           c.ExamPrepHoursPerDay AS CourseExamPrepHoursPerDay, c.ExamPrepDays AS CourseExamPrepDays,
           CASE WHEN st.TaskId IS NOT NULL THEN 1 ELSE 0 END AS HasSharedTask
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON c.CourseId = t.CourseId
    LEFT JOIN SmartStudy_SharedTasks st ON st.TaskId = t.TaskId
    WHERE t.Email = @Email AND t.IsCompleted = 0 AND t.DueDate IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM SmartStudy_Tasks sub WHERE sub.ParentTaskId = t.TaskId)
    ORDER BY t.DueDate;
END
GO

IF OBJECT_ID('SS_Tasks_GetAllIncomplete','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetAllIncomplete;
GO
CREATE PROCEDURE SS_Tasks_GetAllIncomplete
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId, t.CourseId, c.CourseName, t.Title, t.Type, t.EstimatedHours,
           t.DueDate, t.IsCompleted, t.Priority, t.ActualHours, t.ParentTaskId,
           t.AllowSplitting, t.IsManuallyPinned, t.IsManualPriority, t.Email,
           c.Credits AS CourseCredits, c.DefaultTaskEstimatedHours,
           c.ExamPrepHoursPerDay AS CourseExamPrepHoursPerDay, c.ExamPrepDays AS CourseExamPrepDays,
           CASE WHEN st.TaskId IS NOT NULL THEN 1 ELSE 0 END AS HasSharedTask,
           (SELECT COUNT(*) FROM SmartStudy_Tasks sub WHERE sub.ParentTaskId = t.TaskId) AS SubTaskCount,
           (SELECT COUNT(*) FROM SmartStudy_TaskEvents te2 INNER JOIN SmartStudy_Events e2 ON e2.EventId = te2.EventId WHERE te2.TaskId = t.TaskId) AS TaskEventCount,
           CASE WHEN EXISTS(SELECT 1 FROM SmartStudy_TaskEvents te3 WHERE te3.TaskId = t.TaskId AND te3.Status = 'NeedReview') THEN 1 ELSE 0 END AS HasNeedReview
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON c.CourseId = t.CourseId
    LEFT JOIN SmartStudy_SharedTasks st ON st.TaskId = t.TaskId
    WHERE t.Email = @Email AND t.IsCompleted = 0
    ORDER BY t.DueDate;
END
GO

IF OBJECT_ID('SS_Tasks_GetPinnedIds','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetPinnedIds;
GO
CREATE PROCEDURE SS_Tasks_GetPinnedIds
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TaskId FROM SmartStudy_Tasks WHERE Email = @Email AND IsManuallyPinned = 1;
END
GO

IF OBJECT_ID('SS_TaskEvents_GetNeedReviewTaskIds','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_GetNeedReviewTaskIds;
GO
CREATE PROCEDURE SS_TaskEvents_GetNeedReviewTaskIds
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    -- Tasks with events still awaiting approval (either this user's pending review,
    -- or a shared task awaiting the partner). Both states keep the task out of
    -- the auto-scheduler so its slot is preserved.
    SELECT DISTINCT te.TaskId
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    WHERE t.Email = @Email AND te.Status IN ('NeedReview', 'PartiallyApproved');
END
GO

IF OBJECT_ID('SS_TaskEvents_GetByUserAndStatus','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_GetByUserAndStatus;
GO
CREATE PROCEDURE SS_TaskEvents_GetByUserAndStatus
    @Email NVARCHAR(255),
    @Status1 NVARCHAR(50) = NULL,
    @Status2 NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT te.EventId, e.Email, e.[From], e.[To], e.Recurring, e.RecurrenceEndDate,
           te.TaskId, te.Priority, te.ActualHours, te.Status
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    WHERE t.Email = @Email
      AND (te.Status = @Status1 OR te.Status = @Status2)
    ORDER BY e.[From];
END
GO


IF OBJECT_ID('SS_TaskEvents_GetByTaskIdsAndStatuses','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_GetByTaskIdsAndStatuses;
GO
CREATE PROCEDURE SS_TaskEvents_GetByTaskIdsAndStatuses
    @TaskId1 INT,
    @TaskId2 INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT te.EventId, e.Email, e.[From], e.[To], e.Recurring, e.RecurrenceEndDate,
           te.TaskId, te.Priority, te.ActualHours, te.Status
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    WHERE (te.TaskId = @TaskId1 OR te.TaskId = @TaskId2)
      AND te.Status IN ('Scheduled', 'Partial', 'NeedReview', 'PartiallyApproved')
    ORDER BY e.[From];
END
GO

IF OBJECT_ID('SS_Events_GetTimeRange','P') IS NOT NULL DROP PROCEDURE SS_Events_GetTimeRange;
GO
CREATE PROCEDURE SS_Events_GetTimeRange
    @EventId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [From], [To] FROM SmartStudy_Events WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_SharedTasks_GetPartnerTaskId','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_GetPartnerTaskId;
GO
CREATE PROCEDURE SS_SharedTasks_GetPartnerTaskId
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @PartnerTaskId INT = NULL;

    SELECT TOP 1 @PartnerTaskId = stm.CopyTaskId
    FROM SmartStudy_SharedTasks st
    INNER JOIN SmartStudy_SharedTaskMembers stm
        ON stm.TaskId = st.TaskId
       AND stm.CopyTaskId IS NOT NULL
       AND stm.Email <> st.CreatedByEmail
    WHERE st.TaskId = @TaskId
      AND st.SharedStatus = 'Confirmed';

    IF @PartnerTaskId IS NULL
    BEGIN
        SELECT TOP 1 @PartnerTaskId = st.TaskId
        FROM SmartStudy_SharedTaskMembers stm
        INNER JOIN SmartStudy_SharedTasks st ON st.TaskId = stm.TaskId
        WHERE stm.CopyTaskId = @TaskId
          AND st.SharedStatus = 'Confirmed';
    END

    SELECT @PartnerTaskId AS PartnerTaskId;
END
GO

IF OBJECT_ID('SS_TaskEvents_SyncSharedMove','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_SyncSharedMove;
GO
CREATE PROCEDURE SS_TaskEvents_SyncSharedMove
    @MovedEventId  INT,
    @PartnerTaskId INT,
    @OldFrom       DATETIME2,
    @OldTo         DATETIME2,
    @NewFrom       DATETIME2,
    @NewTo         DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PartnerEventId INT;
    SELECT TOP 1 @PartnerEventId = e.EventId
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    WHERE te.TaskId = @PartnerTaskId
      AND e.EventId <> @MovedEventId
      AND e.[From] = @OldFrom
      AND e.[To]   = @OldTo
    ORDER BY e.EventId;

    IF @PartnerEventId IS NOT NULL
    BEGIN
        UPDATE SmartStudy_Events
        SET [From] = @NewFrom, [To] = @NewTo
        WHERE EventId = @PartnerEventId;
    END

    SELECT @PartnerEventId AS PartnerEventId;
END
GO

IF OBJECT_ID('SS_TaskEvents_GetByUserInRange','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_GetByUserInRange;
GO
CREATE PROCEDURE SS_TaskEvents_GetByUserInRange
    @Email NVARCHAR(255),
    @From DATETIME2,
    @To DATETIME2,
    @Status NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT te.EventId, e.Email, e.[From], e.[To], e.Recurring, e.RecurrenceEndDate,
           te.TaskId, te.Priority, te.ActualHours, te.Status
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    WHERE t.Email = @Email AND e.[From] >= @From AND e.[From] < @To
      AND (@Status IS NULL OR te.Status = @Status)
    ORDER BY e.[From];
END
GO

IF OBJECT_ID('SS_Events_DeleteById','P') IS NOT NULL DROP PROCEDURE SS_Events_DeleteById;
GO
CREATE PROCEDURE SS_Events_DeleteById
    @EventId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM SmartStudy_TaskEvents WHERE EventId = @EventId;
    DELETE FROM SmartStudy_ClassEvents WHERE EventId = @EventId;
    DELETE FROM SmartStudy_WorkEvents WHERE EventId = @EventId;
    DELETE FROM SmartStudy_PersonalEvents WHERE EventId = @EventId;
    DELETE FROM SmartStudy_Events WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_ClassEvents_GetIdsByUser','P') IS NOT NULL DROP PROCEDURE SS_ClassEvents_GetIdsByUser;
GO
CREATE PROCEDURE SS_ClassEvents_GetIdsByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ce.EventId FROM SmartStudy_ClassEvents ce
    INNER JOIN SmartStudy_Events e ON e.EventId = ce.EventId
    WHERE e.Email = @Email;
END
GO

IF OBJECT_ID('SS_WorkEvents_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_WorkEvents_GetByUser;
GO
CREATE PROCEDURE SS_WorkEvents_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT we.EventId, e.Email, e.[From], e.[To], e.Recurring, e.RecurrenceEndDate,
           we.WorkPlace, we.TravelTime
    FROM SmartStudy_WorkEvents we
    INNER JOIN SmartStudy_Events e ON e.EventId = we.EventId
    WHERE e.Email = @Email;
END
GO

IF OBJECT_ID('SS_PersonalEvents_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_PersonalEvents_GetByUser;
GO
CREATE PROCEDURE SS_PersonalEvents_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pe.EventId, e.Email, e.[From], e.[To], e.Recurring, e.RecurrenceEndDate,
           pe.Type, pe.[Description]
    FROM SmartStudy_PersonalEvents pe
    INNER JOIN SmartStudy_Events e ON e.EventId = pe.EventId
    WHERE e.Email = @Email;
END
GO

IF OBJECT_ID('SS_Exams_GetForScheduling','P') IS NOT NULL DROP PROCEDURE SS_Exams_GetForScheduling;
GO
CREATE PROCEDURE SS_Exams_GetForScheduling
    @Email NVARCHAR(255),
    @RangeStart DATETIME2,
    @RangeEnd DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.CourseId, c.CourseName, e.Date, e.Time, e.Session, e.Duration, e.IsTakingExam,
           c.ExamPrepHoursPerDay AS CourseExamPrepHoursPerDay, c.ExamPrepDays AS CourseExamPrepDays
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_Courses c ON c.CourseId = e.CourseId
    INNER JOIN SmartStudy_UserCourses uc ON uc.CourseId = e.CourseId AND uc.Email = @Email
    WHERE e.Date >= @RangeStart AND e.Date <= @RangeEnd AND e.IsTakingExam = 1;
END
GO

IF OBJECT_ID('SS_Exams_GetUpcoming','P') IS NOT NULL DROP PROCEDURE SS_Exams_GetUpcoming;
GO
CREATE PROCEDURE SS_Exams_GetUpcoming
    @Email NVARCHAR(255),
    @FromDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.CourseId, c.CourseName, e.Date, e.Time, e.Session, e.Duration, e.IsTakingExam
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_Courses c ON c.CourseId = e.CourseId
    INNER JOIN SmartStudy_UserCourses uc ON uc.CourseId = e.CourseId AND uc.Email = @Email
    WHERE e.Date >= @FromDate;
END
GO

IF OBJECT_ID('SS_Tasks_GetCompletedForML','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetCompletedForML;
GO
CREATE PROCEDURE SS_Tasks_GetCompletedForML
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CourseId, CAST(ActualHours AS FLOAT) AS ActualHours, CAST(EstimatedHours AS FLOAT) AS EstimatedHours
    FROM SmartStudy_Tasks
    WHERE Email = @Email AND IsCompleted = 1
      AND ActualHours IS NOT NULL AND EstimatedHours IS NOT NULL AND EstimatedHours > 0;
END
GO

IF OBJECT_ID('SS_Tasks_GetStudyForExam','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetStudyForExam;
GO
CREATE PROCEDURE SS_Tasks_GetStudyForExam
    @Email NVARCHAR(255),
    @CourseId INT,
    @DueDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 TaskId, CourseId, Title, Type, EstimatedHours, DueDate, IsCompleted, Priority, AllowSplitting, Email
    FROM SmartStudy_Tasks
    WHERE Email = @Email AND CourseId = @CourseId AND Type = 'Study for exam'
      AND DueDate = @DueDate AND IsCompleted = 0;
END
GO

IF OBJECT_ID('SS_Tasks_GetOrphanedStudyTasks','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetOrphanedStudyTasks;
GO
CREATE PROCEDURE SS_Tasks_GetOrphanedStudyTasks
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId
    FROM SmartStudy_Tasks t
    WHERE t.Email = @Email AND t.Type = 'Study for exam' AND t.IsCompleted = 0;
END
GO


IF OBJECT_ID('SS_Tasks_DeleteWithEvents','P') IS NOT NULL DROP PROCEDURE SS_Tasks_DeleteWithEvents;
GO
CREATE PROCEDURE SS_Tasks_DeleteWithEvents
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @EventIds TABLE (EventId INT);
    INSERT INTO @EventIds SELECT EventId FROM SmartStudy_TaskEvents WHERE TaskId = @TaskId;
    DELETE FROM SmartStudy_TaskEvents WHERE TaskId = @TaskId;
    DELETE FROM SmartStudy_Events WHERE EventId IN (SELECT EventId FROM @EventIds);
    DELETE FROM SmartStudy_Tasks WHERE TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_Tasks_UpdatePriority','P') IS NOT NULL DROP PROCEDURE SS_Tasks_UpdatePriority;
GO
CREATE PROCEDURE SS_Tasks_UpdatePriority
    @TaskId INT,
    @Priority NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Tasks SET Priority = @Priority WHERE TaskId = @TaskId;
END
GO

IF OBJECT_ID('SS_Tasks_FindByMatch','P') IS NOT NULL DROP PROCEDURE SS_Tasks_FindByMatch;
GO
CREATE PROCEDURE SS_Tasks_FindByMatch
    @Email NVARCHAR(255),
    @Title NVARCHAR(500),
    @CourseId INT,
    @DueDate DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 TaskId, CourseId, Title, Type, EstimatedHours, DueDate, IsCompleted, Priority, AllowSplitting, Email
    FROM SmartStudy_Tasks
    WHERE Email = @Email AND Title = @Title AND CourseId = @CourseId
      AND ((@DueDate IS NULL AND DueDate IS NULL) OR DueDate = @DueDate)
      AND IsCompleted = 0;
END
GO

IF OBJECT_ID('SS_PersonalEvents_FindByGcalId','P') IS NOT NULL DROP PROCEDURE SS_PersonalEvents_FindByGcalId;
GO
CREATE PROCEDURE SS_PersonalEvents_FindByGcalId
    @Email NVARCHAR(255),
    @GcalMarker NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 pe.EventId, e.[From], e.[To], pe.Type, pe.[Description]
    FROM SmartStudy_PersonalEvents pe
    INNER JOIN SmartStudy_Events e ON e.EventId = pe.EventId
    WHERE e.Email = @Email
      AND pe.[Description] IS NOT NULL
      AND CHARINDEX(@GcalMarker, pe.[Description]) > 0;
END
GO

IF OBJECT_ID('SS_Events_UpdateTimes','P') IS NOT NULL DROP PROCEDURE SS_Events_UpdateTimes;
GO
CREATE PROCEDURE SS_Events_UpdateTimes
    @EventId INT,
    @From DATETIME2,
    @To DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Events SET [From] = @From, [To] = @To WHERE EventId = @EventId;
END
GO

IF OBJECT_ID('SS_Users_UpdateCalendarFields','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateCalendarFields;
GO
CREATE PROCEDURE SS_Users_UpdateCalendarFields
    @Email NVARCHAR(255),
    @GoogleCalendarAccessToken NVARCHAR(MAX) = NULL,
    @GoogleCalendarRefreshToken NVARCHAR(MAX) = NULL,
    @LastCalendarSync DATETIME2 = NULL,
    @ComposioConnectedAccountId NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET GoogleCalendarAccessToken = @GoogleCalendarAccessToken,
        GoogleCalendarRefreshToken = @GoogleCalendarRefreshToken,
        LastCalendarSync = @LastCalendarSync,
        ComposioConnectedAccountId = @ComposioConnectedAccountId
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_PersonalEvents_CountGcal','P') IS NOT NULL DROP PROCEDURE SS_PersonalEvents_CountGcal;
GO
CREATE PROCEDURE SS_PersonalEvents_CountGcal
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*)
    FROM SmartStudy_PersonalEvents pe
    INNER JOIN SmartStudy_Events e ON e.EventId = pe.EventId
    WHERE e.Email = @Email AND pe.[Description] IS NOT NULL AND pe.[Description] LIKE '%[[]gcal:%';
END
GO

IF OBJECT_ID('SS_PersonalEvents_GetGcalEventIds','P') IS NOT NULL DROP PROCEDURE SS_PersonalEvents_GetGcalEventIds;
GO
CREATE PROCEDURE SS_PersonalEvents_GetGcalEventIds
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pe.EventId
    FROM SmartStudy_PersonalEvents pe
    INNER JOIN SmartStudy_Events e ON e.EventId = pe.EventId
    WHERE e.Email = @Email AND pe.[Description] IS NOT NULL AND pe.[Description] LIKE '%[[]gcal:%';
END
GO

IF OBJECT_ID('SS_ClassEvents_Exists','P') IS NOT NULL DROP PROCEDURE SS_ClassEvents_Exists;
GO
CREATE PROCEDURE SS_ClassEvents_Exists
    @Email NVARCHAR(255),
    @CourseId INT,
    @From DATETIME2,
    @To DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (
        SELECT 1 FROM SmartStudy_ClassEvents ce
        INNER JOIN SmartStudy_Events e ON e.EventId = ce.EventId
        WHERE e.Email = @Email AND ce.CourseId = @CourseId AND e.[From] = @From AND e.[To] = @To
    ) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_Instructors_FindByName','P') IS NOT NULL DROP PROCEDURE SS_Instructors_FindByName;
GO
CREATE PROCEDURE SS_Instructors_FindByName
    @Name NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 InstructorId, InstructorName FROM SmartStudy_Instructors WHERE InstructorName = @Name;
END
GO

IF OBJECT_ID('SS_Instructors_Create','P') IS NOT NULL DROP PROCEDURE SS_Instructors_Create;
GO
CREATE PROCEDURE SS_Instructors_Create
    @Name NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Instructors (InstructorName) VALUES (@Name);
    SELECT SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('SS_Exams_FindByCourseAndSession','P') IS NOT NULL DROP PROCEDURE SS_Exams_FindByCourseAndSession;
GO
CREATE PROCEDURE SS_Exams_FindByCourseAndSession
    @Email NVARCHAR(255),
    @CourseId INT,
    @Session NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 e.ExamId, e.CourseId, e.Date, e.Time, e.Session, e.Duration, e.IsTakingExam
    FROM SmartStudy_Exams e
    WHERE e.CourseId = @CourseId AND e.Session = @Session;
END
GO

IF OBJECT_ID('SS_Exams_UpdateFull','P') IS NOT NULL DROP PROCEDURE SS_Exams_UpdateFull;
GO
CREATE PROCEDURE SS_Exams_UpdateFull
    @ExamId INT,
    @Date DATETIME2,
    @Time TIME,
    @Duration INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Exams SET Date = @Date, Time = @Time, Duration = @Duration WHERE ExamId = @ExamId;
END
GO

IF OBJECT_ID('SS_Courses_GetAll','P') IS NOT NULL DROP PROCEDURE SS_Courses_GetAll;
GO
CREATE PROCEDURE SS_Courses_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CourseId, CourseName, WeeklyHours, Credits, Semester, InstructorId,
           DefaultTaskEstimatedHours, ExamPrepHoursPerDay, ExamPrepDays
    FROM SmartStudy_Courses;
END
GO

IF OBJECT_ID('SS_Courses_Exists','P') IS NOT NULL DROP PROCEDURE SS_Courses_Exists;
GO
CREATE PROCEDURE SS_Courses_Exists
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM SmartStudy_Courses WHERE CourseId = @CourseId) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_UserCourses_GetWithCourseName','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_GetWithCourseName;
GO
CREATE PROCEDURE SS_UserCourses_GetWithCourseName
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT uc.CourseId, c.CourseName
    FROM SmartStudy_UserCourses uc
    INNER JOIN SmartStudy_Courses c ON c.CourseId = uc.CourseId
    WHERE uc.Email = @Email;
END
GO

IF OBJECT_ID('SS_ClassEvents_ReassignCourse','P') IS NOT NULL DROP PROCEDURE SS_ClassEvents_ReassignCourse;
GO
CREATE PROCEDURE SS_ClassEvents_ReassignCourse
    @Email NVARCHAR(255),
    @FromCourseId INT,
    @ToCourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DupEventIds TABLE (EventId INT);
    INSERT INTO @DupEventIds
    SELECT ce.EventId FROM SmartStudy_ClassEvents ce
    INNER JOIN SmartStudy_Events e ON e.EventId = ce.EventId
    WHERE e.Email = @Email AND ce.CourseId = @FromCourseId
      AND EXISTS (
          SELECT 1 FROM SmartStudy_ClassEvents ce2
          INNER JOIN SmartStudy_Events e2 ON e2.EventId = ce2.EventId
          WHERE e2.Email = @Email AND ce2.CourseId = @ToCourseId
            AND e2.[From] = e.[From] AND e2.[To] = e.[To]
      );
    DELETE FROM SmartStudy_ClassEvents WHERE EventId IN (SELECT EventId FROM @DupEventIds);
    DELETE FROM SmartStudy_Events WHERE EventId IN (SELECT EventId FROM @DupEventIds);
    UPDATE ce SET ce.CourseId = @ToCourseId
    FROM SmartStudy_ClassEvents ce
    INNER JOIN SmartStudy_Events e ON e.EventId = ce.EventId
    WHERE e.Email = @Email AND ce.CourseId = @FromCourseId;
END
GO

IF OBJECT_ID('SS_Exams_ReassignCourse','P') IS NOT NULL DROP PROCEDURE SS_Exams_ReassignCourse;
GO
CREATE PROCEDURE SS_Exams_ReassignCourse
    @FromCourseId INT,
    @ToCourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM SmartStudy_Exams
    WHERE CourseId = @FromCourseId
      AND Session IN (SELECT Session FROM SmartStudy_Exams WHERE CourseId = @ToCourseId);
    UPDATE SmartStudy_Exams SET CourseId = @ToCourseId WHERE CourseId = @FromCourseId;
END
GO

IF OBJECT_ID('SS_Tasks_ReassignCourse','P') IS NOT NULL DROP PROCEDURE SS_Tasks_ReassignCourse;
GO
CREATE PROCEDURE SS_Tasks_ReassignCourse
    @Email NVARCHAR(255),
    @FromCourseId INT,
    @ToCourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Tasks SET CourseId = @ToCourseId
    WHERE Email = @Email AND CourseId = @FromCourseId;
END
GO

IF OBJECT_ID('SS_UserCourses_OtherUsersExist','P') IS NOT NULL DROP PROCEDURE SS_UserCourses_OtherUsersExist;
GO
CREATE PROCEDURE SS_UserCourses_OtherUsersExist
    @CourseId INT,
    @ExcludeEmail NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (
        SELECT 1 FROM SmartStudy_UserCourses WHERE CourseId = @CourseId AND Email != @ExcludeEmail
    ) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('SS_Courses_Delete','P') IS NOT NULL DROP PROCEDURE SS_Courses_Delete;
GO
CREATE PROCEDURE SS_Courses_Delete
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM SmartStudy_Courses WHERE CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_Users_GetForRuppinetSync','P') IS NOT NULL DROP PROCEDURE SS_Users_GetForRuppinetSync;
GO
CREATE PROCEDURE SS_Users_GetForRuppinetSync
    @Cutoff DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email FROM SmartStudy_Users
    WHERE RuppinetId IS NOT NULL AND RuppinetPassword IS NOT NULL
      AND (LastRuppinetSync IS NULL OR LastRuppinetSync < @Cutoff);
END
GO

IF OBJECT_ID('SS_Users_UpdateLastCalendarSync','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateLastCalendarSync;
GO
CREATE PROCEDURE SS_Users_UpdateLastCalendarSync
    @Email NVARCHAR(255),
    @LastCalendarSync DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users SET LastCalendarSync = @LastCalendarSync WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_UpdateLastRuppinetSync','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateLastRuppinetSync;
GO
CREATE PROCEDURE SS_Users_UpdateLastRuppinetSync
    @Email NVARCHAR(255),
    @LastRuppinetSync DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users SET LastRuppinetSync = @LastRuppinetSync WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_UpdateComposioId','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateComposioId;
GO
CREATE PROCEDURE SS_Users_UpdateComposioId
    @Email NVARCHAR(255),
    @ComposioConnectedAccountId NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users SET ComposioConnectedAccountId = @ComposioConnectedAccountId WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Courses_UpdateInstructor','P') IS NOT NULL DROP PROCEDURE SS_Courses_UpdateInstructor;
GO
CREATE PROCEDURE SS_Courses_UpdateInstructor
    @CourseId INT,
    @InstructorId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Courses SET InstructorId = @InstructorId WHERE CourseId = @CourseId;
END
GO

IF OBJECT_ID('SS_Tasks_CountByUser','P') IS NOT NULL DROP PROCEDURE SS_Tasks_CountByUser;
GO
CREATE PROCEDURE SS_Tasks_CountByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM SmartStudy_Tasks WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_TaskEvents_Approve','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_Approve;
GO
CREATE PROCEDURE SS_TaskEvents_Approve
    @TaskId INT,
    @Email NVARCHAR(255),
    @Now DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    -- Figure out if this task is part of a shared task, and if so, find the
    -- partner's task id (via SharedTaskMembers.CopyTaskId linkage).
    DECLARE @CreatorTaskId INT = NULL;
    DECLARE @PartnerTaskId INT = NULL;

    -- Scenario A: @TaskId is the creator's task
    IF EXISTS (SELECT 1 FROM SmartStudy_SharedTasks
               WHERE TaskId = @TaskId AND SharedStatus = 'Confirmed')
    BEGIN
        SET @CreatorTaskId = @TaskId;
        SELECT TOP 1 @PartnerTaskId = stm.CopyTaskId
        FROM SmartStudy_SharedTaskMembers stm
        WHERE stm.TaskId = @TaskId
          AND stm.CopyTaskId IS NOT NULL
          AND stm.Email <> @Email;
    END
    ELSE
    BEGIN
        -- Scenario B: @TaskId is a partner's copy
        SELECT TOP 1 @CreatorTaskId = stm.TaskId, @PartnerTaskId = stm.CopyTaskId
        FROM SmartStudy_SharedTaskMembers stm
        INNER JOIN SmartStudy_SharedTasks st ON st.TaskId = stm.TaskId
        WHERE stm.CopyTaskId = @TaskId
          AND st.SharedStatus = 'Confirmed';
    END

    DECLARE @ApprovedCount INT = 0;
    DECLARE @IsShared BIT = CASE WHEN @CreatorTaskId IS NOT NULL AND @PartnerTaskId IS NOT NULL THEN 1 ELSE 0 END;

    IF @IsShared = 1
    BEGIN
        -- Shared task: flip MY NeedReview events to an intermediate status so
        -- they stay "Pending" on the calendar until the partner also approves.
        UPDATE te SET te.Status = 'PartiallyApproved'
        FROM SmartStudy_TaskEvents te
        INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
        WHERE te.TaskId = @TaskId AND t.Email = @Email AND te.Status = 'NeedReview';

        SET @ApprovedCount = @@ROWCOUNT;

        -- Determine partner's TaskId (the other side).
        DECLARE @OtherTaskId INT =
            CASE WHEN @TaskId = @CreatorTaskId THEN @PartnerTaskId ELSE @CreatorTaskId END;

        DECLARE @PartnerPending INT;
        SELECT @PartnerPending = COUNT(*)
        FROM SmartStudy_TaskEvents
        WHERE TaskId = @OtherTaskId AND Status = 'NeedReview';

        DECLARE @PartnerApproved INT;
        SELECT @PartnerApproved = COUNT(*)
        FROM SmartStudy_TaskEvents
        WHERE TaskId = @OtherTaskId AND Status = 'PartiallyApproved';

        -- Partner already approved → no NeedReview remaining, at least one PartiallyApproved.
        -- Finalise both sides to Scheduled, removing the Pending tag.
        IF @PartnerPending = 0 AND @PartnerApproved > 0
        BEGIN
            UPDATE SmartStudy_TaskEvents SET Status = 'Scheduled'
            WHERE TaskId IN (@CreatorTaskId, @PartnerTaskId) AND Status = 'PartiallyApproved';
        END
    END
    ELSE
    BEGIN
        -- Non-shared task: approve immediately (original behaviour).
        UPDATE te SET te.Status = 'Scheduled'
        FROM SmartStudy_TaskEvents te
        INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
        WHERE te.TaskId = @TaskId AND t.Email = @Email AND te.Status = 'NeedReview';

        SET @ApprovedCount = @@ROWCOUNT;
    END

    DECLARE @PastEventIds TABLE (EventId INT);
    INSERT INTO @PastEventIds
    SELECT te.EventId
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    WHERE te.TaskId = @TaskId AND e.[From] < @Now
      AND te.Status NOT IN ('NeedReview', 'PartiallyApproved');

    DECLARE @RemovedPast INT = (SELECT COUNT(*) FROM @PastEventIds);
    DELETE FROM SmartStudy_TaskEvents WHERE EventId IN (SELECT EventId FROM @PastEventIds);
    DELETE FROM SmartStudy_Events WHERE EventId IN (SELECT EventId FROM @PastEventIds);

    SELECT @ApprovedCount AS ApprovedCount, @RemovedPast AS RemovedPast;
END
GO

IF OBJECT_ID('SS_Users_GetAllEmails','P') IS NOT NULL DROP PROCEDURE SS_Users_GetAllEmails;
GO
CREATE PROCEDURE SS_Users_GetAllEmails
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email FROM SmartStudy_Users;
END
GO

IF OBJECT_ID('SS_Users_UpdateGoogleToken','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateGoogleToken;
GO
CREATE PROCEDURE SS_Users_UpdateGoogleToken
    @Email NVARCHAR(255),
    @GoogleCalendarAccessToken NVARCHAR(MAX),
    @LastCalendarSync DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET GoogleCalendarAccessToken = @GoogleCalendarAccessToken, LastCalendarSync = @LastCalendarSync
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_DisconnectGoogleCalendar','P') IS NOT NULL DROP PROCEDURE SS_Users_DisconnectGoogleCalendar;
GO
CREATE PROCEDURE SS_Users_DisconnectGoogleCalendar
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET GoogleCalendarAccessToken = NULL, GoogleCalendarRefreshToken = NULL, LastCalendarSync = NULL, ComposioConnectedAccountId = NULL
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_GetByComposioId','P') IS NOT NULL DROP PROCEDURE SS_Users_GetByComposioId;
GO
CREATE PROCEDURE SS_Users_GetByComposioId
    @ComposioConnectedAccountId NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email FROM SmartStudy_Users WHERE ComposioConnectedAccountId = @ComposioConnectedAccountId;
END
GO

IF OBJECT_ID('SS_Users_ClearLastCalendarSync','P') IS NOT NULL DROP PROCEDURE SS_Users_ClearLastCalendarSync;
GO
CREATE PROCEDURE SS_Users_ClearLastCalendarSync
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users SET LastCalendarSync = NULL WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Tasks_GetIncompleteWithEvents','P') IS NOT NULL DROP PROCEDURE SS_Tasks_GetIncompleteWithEvents;
GO
CREATE PROCEDURE SS_Tasks_GetIncompleteWithEvents
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TaskId, t.CourseId, c.CourseName, t.Title, t.Type, t.EstimatedHours,
           t.DueDate, t.IsCompleted, t.Priority, t.ActualHours, t.ParentTaskId,
           t.AllowSplitting, t.IsManuallyPinned, t.IsManualPriority, t.Email,
           c.Credits AS CourseCredits, c.DefaultTaskEstimatedHours,
           CASE WHEN st.TaskId IS NOT NULL THEN st.SharedStatus ELSE NULL END AS SharedStatus
    FROM SmartStudy_Tasks t
    INNER JOIN SmartStudy_Courses c ON c.CourseId = t.CourseId
    LEFT JOIN SmartStudy_SharedTasks st ON st.TaskId = t.TaskId
    WHERE t.Email = @Email AND t.IsCompleted = 0
    ORDER BY t.DueDate;

    SELECT te.TaskId, te.EventId, e.[From], e.[To], te.Status
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Events e ON e.EventId = te.EventId
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    WHERE t.Email = @Email AND t.IsCompleted = 0
    ORDER BY te.TaskId, e.[From];
END
GO

-- ===== MOODLE INTEGRATION =====

IF OBJECT_ID('SS_Users_UpdateMoodleFields','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateMoodleFields;
GO
CREATE PROCEDURE SS_Users_UpdateMoodleFields
    @Email NVARCHAR(255),
    @MoodleToken NVARCHAR(500) = NULL,
    @LastMoodleSync DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET MoodleToken = @MoodleToken, LastMoodleSync = @LastMoodleSync
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_ClearMoodle','P') IS NOT NULL DROP PROCEDURE SS_Users_ClearMoodle;
GO
CREATE PROCEDURE SS_Users_ClearMoodle
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users
    SET MoodleToken = NULL, LastMoodleSync = NULL
    WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Users_GetForMoodleSync','P') IS NOT NULL DROP PROCEDURE SS_Users_GetForMoodleSync;
GO
CREATE PROCEDURE SS_Users_GetForMoodleSync
    @Cutoff DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Email FROM SmartStudy_Users
    WHERE RuppinetId IS NOT NULL AND RuppinetPassword IS NOT NULL
      AND (LastMoodleSync IS NULL OR LastMoodleSync < @Cutoff);
END
GO

IF OBJECT_ID('SS_Users_UpdateLastMoodleSync','P') IS NOT NULL DROP PROCEDURE SS_Users_UpdateLastMoodleSync;
GO
CREATE PROCEDURE SS_Users_UpdateLastMoodleSync
    @Email NVARCHAR(255),
    @LastMoodleSync DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Users SET LastMoodleSync = @LastMoodleSync WHERE Email = @Email;
END
GO

IF OBJECT_ID('SS_Tasks_FindByMoodleId','P') IS NOT NULL DROP PROCEDURE SS_Tasks_FindByMoodleId;
GO
CREATE PROCEDURE SS_Tasks_FindByMoodleId
    @Email NVARCHAR(255),
    @MoodleId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 TaskId, CourseId, Title, [Type], EstimatedHours, DueDate, IsCompleted, [Priority], Email, MoodleId
    FROM SmartStudy_Tasks
    WHERE Email = @Email AND MoodleId = @MoodleId;
END
GO

IF OBJECT_ID('SS_Tasks_CreateWithMoodleId','P') IS NOT NULL DROP PROCEDURE SS_Tasks_CreateWithMoodleId;
GO
CREATE PROCEDURE SS_Tasks_CreateWithMoodleId
    @CourseId INT,
    @Email NVARCHAR(255),
    @Title NVARCHAR(200),
    @Type NVARCHAR(50),
    @EstimatedHours DECIMAL(5,2) = NULL,
    @DueDate DATETIME2 = NULL,
    @ParentTaskId INT = NULL,
    @AllowSplitting BIT = 0,
    @Priority NVARCHAR(20) = NULL,
    @IsManualPriority BIT = 0,
    @IsManuallyPinned BIT = 0,
    @MoodleId NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Tasks (CourseId, Email, Title, [Type], EstimatedHours, DueDate, IsCompleted,
        ParentTaskId, AllowSplitting, [Priority], IsManualPriority, IsManuallyPinned, MoodleId)
    VALUES (@CourseId, @Email, @Title, @Type, @EstimatedHours, @DueDate, 0,
        @ParentTaskId, @AllowSplitting, @Priority, @IsManualPriority, @IsManuallyPinned, @MoodleId);
    SELECT SCOPE_IDENTITY();
END
GO

PRINT 'Stored procedures created successfully.';
