using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartStudy.DAL;
using SmartStudy.Data;
using SmartStudy.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - SQL Server
var connectionString = builder.Configuration.GetConnectionString("SmartStudyDb");
builder.Services.AddDbContext<SmartStudyDbContext>(options =>
    options.UseSqlServer(connectionString));

// ADO.NET DAL (stored procedures)
builder.Services.AddSingleton<SqlHelper>();
builder.Services.AddScoped<DBservices>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SmartStudySuperSecretKey2026ForJwtTokenGeneration!";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<StressService>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<ScheduleImportService>();
builder.Services.AddScoped<SafeZoneService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WeeklySuggestionService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ComposioService>();
builder.Services.AddScoped<GoogleCalendarService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<RuppinetApiClient>();
builder.Services.AddScoped<RuppinetSyncService>();
builder.Services.AddScoped<MoodleApiClient>();
builder.Services.AddScoped<MoodleSyncService>();
builder.Services.AddHostedService<NotificationBackgroundService>();
builder.Services.AddHostedService<RuppinetBackgroundSyncService>();
builder.Services.AddHostedService<MoodleBackgroundSyncService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// PathBase for IIS subpath deployment (e.g. /igroup27/test2/tar1)
var pathBase = builder.Configuration["PathBase"] ?? "";
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

// Create tables and seed data on first run (works on shared school DB)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartStudyDbContext>();

    // Check if tables need to be created (works on shared school DB)
    var conn = db.Database.GetDbConnection();
    conn.Open();
    bool needsSetup = false;
    using (var checkCmd = conn.CreateCommand())
    {
        // Check if SmartStudy_Courses exists and has data (courses are the core entity)
        checkCmd.CommandText = @"
            IF OBJECT_ID('SmartStudy_Courses','U') IS NULL SELECT 0
            ELSE SELECT COUNT(*) FROM SmartStudy_Courses";
        var result = Convert.ToInt32(checkCmd.ExecuteScalar());
        needsSetup = (result == 0);
    }

    if (needsSetup)
    {
        // Drop all SmartStudy tables and recreate with correct schema
        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = @"
            IF OBJECT_ID('SmartStudy_Notifications','U') IS NOT NULL DROP TABLE SmartStudy_Notifications;
            IF OBJECT_ID('SmartStudy_SharedTaskMembers','U') IS NOT NULL DROP TABLE SmartStudy_SharedTaskMembers;
            IF OBJECT_ID('SmartStudy_SharedTasks','U') IS NOT NULL DROP TABLE SmartStudy_SharedTasks;
            IF OBJECT_ID('SmartStudy_Friendships','U') IS NOT NULL DROP TABLE SmartStudy_Friendships;
            IF OBJECT_ID('SmartStudy_FriendRequests','U') IS NOT NULL DROP TABLE SmartStudy_FriendRequests;
            IF OBJECT_ID('SmartStudy_StudyConnections','U') IS NOT NULL DROP TABLE SmartStudy_StudyConnections;
            IF OBJECT_ID('SmartStudy_TaskEvents','U') IS NOT NULL DROP TABLE SmartStudy_TaskEvents;
            IF OBJECT_ID('SmartStudy_ClassEvents','U') IS NOT NULL DROP TABLE SmartStudy_ClassEvents;
            IF OBJECT_ID('SmartStudy_WorkEvents','U') IS NOT NULL DROP TABLE SmartStudy_WorkEvents;
            IF OBJECT_ID('SmartStudy_PersonalEvents','U') IS NOT NULL DROP TABLE SmartStudy_PersonalEvents;
            IF OBJECT_ID('SmartStudy_Events','U') IS NOT NULL DROP TABLE SmartStudy_Events;
            IF OBJECT_ID('SmartStudy_Exams','U') IS NOT NULL DROP TABLE SmartStudy_Exams;
            IF OBJECT_ID('SmartStudy_Tasks','U') IS NOT NULL DROP TABLE SmartStudy_Tasks;
            IF OBJECT_ID('SmartStudy_UserCourses','U') IS NOT NULL DROP TABLE SmartStudy_UserCourses;
            IF OBJECT_ID('SmartStudy_Courses','U') IS NOT NULL DROP TABLE SmartStudy_Courses;
            IF OBJECT_ID('SmartStudy_SchedulingPreferences','U') IS NOT NULL DROP TABLE SmartStudy_SchedulingPreferences;
            IF OBJECT_ID('SmartStudy_NotificationSettings','U') IS NOT NULL DROP TABLE SmartStudy_NotificationSettings;
            IF OBJECT_ID('SmartStudy_Users','U') IS NOT NULL DROP TABLE SmartStudy_Users;
            IF OBJECT_ID('SmartStudy_Instructors','U') IS NOT NULL DROP TABLE SmartStudy_Instructors;";
        dropCmd.ExecuteNonQuery();

        var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        creator.CreateTables();
    }
    conn.Close();

    SeedDataService.SeedDatabase(db);

    // Drop unique index on TaskEvents.TaskId if it exists (allow 1:N task splitting)
    conn.Open();
    using (var fixCmd = conn.CreateCommand())
    {
        fixCmd.CommandText = @"
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmartStudy_TaskEvents_TaskId' AND object_id = OBJECT_ID('SmartStudy_TaskEvents'))
            BEGIN
                DROP INDEX IX_SmartStudy_TaskEvents_TaskId ON SmartStudy_TaskEvents;
                CREATE NONCLUSTERED INDEX IX_SmartStudy_TaskEvents_TaskId ON SmartStudy_TaskEvents(TaskId);
            END";
        fixCmd.ExecuteNonQuery();
    }

    // Migrate: drop old StudyConnections, create new FriendRequests + Friendships + SharedTasks + SharedTaskMembers
    using (var migrateCmd = conn.CreateCommand())
    {
        migrateCmd.CommandText = @"
            -- Drop old table if it exists
            IF OBJECT_ID('SmartStudy_StudyConnections','U') IS NOT NULL
                DROP TABLE SmartStudy_StudyConnections;

            -- Create FriendRequests if not exists
            IF OBJECT_ID('SmartStudy_FriendRequests','U') IS NULL
            BEGIN
                CREATE TABLE SmartStudy_FriendRequests (
                    RequestId INT IDENTITY(1,1) PRIMARY KEY,
                    RequesterEmail NVARCHAR(255) NOT NULL,
                    AddresseeEmail NVARCHAR(255) NOT NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    RequestedAt DATETIME2 NOT NULL,
                    RespondedAt DATETIME2 NULL,
                    CONSTRAINT FK_FriendRequests_Requester FOREIGN KEY (RequesterEmail) REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE,
                    CONSTRAINT FK_FriendRequests_Addressee FOREIGN KEY (AddresseeEmail) REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION,
                    CONSTRAINT CK_FriendRequest_NotSelf CHECK (RequesterEmail <> AddresseeEmail)
                );
                CREATE UNIQUE NONCLUSTERED INDEX IX_FriendRequests_Pending
                    ON SmartStudy_FriendRequests(RequesterEmail, AddresseeEmail) WHERE Status = 'Pending';

                -- Seed friend requests
                INSERT INTO SmartStudy_FriendRequests (RequesterEmail, AddresseeEmail, Status, RequestedAt, RespondedAt)
                VALUES
                    ('demo@smartstudy.com', 'sarah.cohen@uni.ac.il', 'Accepted', '2026-01-15', '2026-01-15'),
                    ('demo@smartstudy.com', 'david.levi@uni.ac.il', 'Accepted', '2026-01-22', '2026-01-22'),
                    ('yuval@smartstudy.com', 'sarah.cohen@uni.ac.il', 'Accepted', '2026-01-20', '2026-01-20'),
                    ('maya.alon@uni.ac.il', 'demo@smartstudy.com', 'Pending', '2026-02-05', NULL),
                    ('maya.alon@uni.ac.il', 'yuval@smartstudy.com', 'Pending', '2026-02-06', NULL);
            END

            -- Create Friendships if not exists
            IF OBJECT_ID('SmartStudy_Friendships','U') IS NULL
            BEGIN
                CREATE TABLE SmartStudy_Friendships (
                    FriendshipId INT IDENTITY(1,1) PRIMARY KEY,
                    Email1 NVARCHAR(255) NOT NULL,
                    Email2 NVARCHAR(255) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CONSTRAINT FK_Friendships_User1 FOREIGN KEY (Email1) REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE,
                    CONSTRAINT FK_Friendships_User2 FOREIGN KEY (Email2) REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION,
                    CONSTRAINT CK_Friendship_NotSelf CHECK (Email1 <> Email2)
                );
                CREATE UNIQUE NONCLUSTERED INDEX IX_Friendships_Pair ON SmartStudy_Friendships(Email1, Email2);

                -- Seed friendships (normalized pairs: Email1 < Email2 alphabetically)
                INSERT INTO SmartStudy_Friendships (Email1, Email2, CreatedAt, IsActive)
                VALUES
                    ('demo@smartstudy.com', 'sarah.cohen@uni.ac.il', '2026-01-15', 1),
                    ('david.levi@uni.ac.il', 'demo@smartstudy.com', '2026-01-22', 1),
                    ('sarah.cohen@uni.ac.il', 'yuval@smartstudy.com', '2026-01-20', 1);
            END

            -- Create SharedTasks if not exists
            IF OBJECT_ID('SmartStudy_SharedTasks','U') IS NULL
            BEGIN
                CREATE TABLE SmartStudy_SharedTasks (
                    TaskId INT NOT NULL PRIMARY KEY,
                    CreatedByEmail NVARCHAR(255) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    SharedStatus NVARCHAR(20) NOT NULL DEFAULT 'Draft',
                    CONSTRAINT FK_SharedTasks_Task FOREIGN KEY (TaskId) REFERENCES SmartStudy_Tasks(TaskId) ON DELETE CASCADE,
                    CONSTRAINT FK_SharedTasks_CreatedBy FOREIGN KEY (CreatedByEmail) REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION
                );
            END

            -- Create SharedTaskMembers if not exists
            IF OBJECT_ID('SmartStudy_SharedTaskMembers','U') IS NULL
            BEGIN
                CREATE TABLE SmartStudy_SharedTaskMembers (
                    TaskId INT NOT NULL,
                    Email NVARCHAR(255) NOT NULL,
                    ResponseStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    RespondedAt DATETIME2 NULL,
                    CONSTRAINT PK_SharedTaskMembers PRIMARY KEY (TaskId, Email),
                    CONSTRAINT FK_SharedTaskMembers_SharedTask FOREIGN KEY (TaskId) REFERENCES SmartStudy_SharedTasks(TaskId) ON DELETE CASCADE,
                    CONSTRAINT FK_SharedTaskMembers_User FOREIGN KEY (Email) REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION
                );
            END";
        migrateCmd.ExecuteNonQuery();
    }

    // Phase 1-12 migrations: ActualHours, ParentTaskId, QuietHours, Notifications, StudyPartner, ResetToken, AuthProvider
    using (var phase1Cmd = conn.CreateCommand())
    {
        phase1Cmd.CommandText = @"
            -- Add ActualHours to Tasks
            IF COL_LENGTH('SmartStudy_Tasks', 'ActualHours') IS NULL
                ALTER TABLE SmartStudy_Tasks ADD ActualHours DECIMAL(5,2) NULL;

            -- Add ParentTaskId to Tasks (sub-tasks)
            IF COL_LENGTH('SmartStudy_Tasks', 'ParentTaskId') IS NULL
            BEGIN
                ALTER TABLE SmartStudy_Tasks ADD ParentTaskId INT NULL;
                ALTER TABLE SmartStudy_Tasks ADD CONSTRAINT FK_Tasks_ParentTask
                    FOREIGN KEY (ParentTaskId) REFERENCES SmartStudy_Tasks(TaskId);
                CREATE NONCLUSTERED INDEX IX_Tasks_ParentTaskId ON SmartStudy_Tasks(ParentTaskId);
            END

            -- Add QuietHours to NotificationSettings
            IF COL_LENGTH('SmartStudy_NotificationSettings', 'Quiet_hours_start') IS NULL
                ALTER TABLE SmartStudy_NotificationSettings ADD Quiet_hours_start TIME NULL;
            IF COL_LENGTH('SmartStudy_NotificationSettings', 'Quiet_hours_end') IS NULL
                ALTER TABLE SmartStudy_NotificationSettings ADD Quiet_hours_end TIME NULL;

            -- Create Notifications table
            IF OBJECT_ID('SmartStudy_Notifications','U') IS NULL
            BEGIN
                CREATE TABLE SmartStudy_Notifications (
                    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
                    Email NVARCHAR(255) NOT NULL,
                    Type NVARCHAR(50) NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Message NVARCHAR(1000) NOT NULL,
                    IsRead BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL,
                    RelatedEntityId INT NULL,
                    RelatedEntityType NVARCHAR(50) NULL,
                    CONSTRAINT FK_Notifications_User FOREIGN KEY (Email) REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
                );
                CREATE NONCLUSTERED INDEX IX_Notifications_Email_CreatedAt ON SmartStudy_Notifications(Email, CreatedAt DESC);
            END

            -- Add StudyPartnerEmail to UserCourses
            IF COL_LENGTH('SmartStudy_UserCourses', 'StudyPartnerEmail') IS NULL
                ALTER TABLE SmartStudy_UserCourses ADD StudyPartnerEmail NVARCHAR(255) NULL;

            -- Add ResetToken fields to Users
            IF COL_LENGTH('SmartStudy_Users', 'ResetToken') IS NULL
                ALTER TABLE SmartStudy_Users ADD ResetToken NVARCHAR(50) NULL;
            IF COL_LENGTH('SmartStudy_Users', 'ResetTokenExpiry') IS NULL
                ALTER TABLE SmartStudy_Users ADD ResetTokenExpiry DATETIME2 NULL;

            -- Add AuthProvider to Users
            IF COL_LENGTH('SmartStudy_Users', 'AuthProvider') IS NULL
                ALTER TABLE SmartStudy_Users ADD AuthProvider NVARCHAR(20) NULL;
        ";
        phase1Cmd.ExecuteNonQuery();
    }

    // Migrate scheduling preferences to separate table
    using (var schedCmd = conn.CreateCommand())
    {
        schedCmd.CommandText = @"
            -- Ensure OnboardingCompleted column exists on Users
            IF COL_LENGTH('SmartStudy_Users', 'OnboardingCompleted') IS NULL
                ALTER TABLE SmartStudy_Users ADD OnboardingCompleted BIT NOT NULL DEFAULT 0;

            -- Create SchedulingPreferences table if not exists
            IF OBJECT_ID('SmartStudy_SchedulingPreferences','U') IS NULL
            BEGIN
                CREATE TABLE SmartStudy_SchedulingPreferences (
                    Email NVARCHAR(255) NOT NULL PRIMARY KEY,
                    MaxDailyStudyHours FLOAT NOT NULL DEFAULT 6.0,
                    MaxContinuousMinutes INT NOT NULL DEFAULT 90,
                    DayStartHour INT NOT NULL DEFAULT 8,
                    DayEndHour INT NOT NULL DEFAULT 22,
                    SleepHoursPerDay FLOAT NOT NULL DEFAULT 8.0,
                    LunchBreakStart TIME NULL,
                    LunchBreakEnd TIME NULL,
                    CONSTRAINT FK_SchedulingPreferences_User FOREIGN KEY (Email) REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
                );

                -- Migrate existing data from Users columns if they exist (dynamic SQL to avoid compile-time column validation)
                IF COL_LENGTH('SmartStudy_Users', 'MaxDailyStudyHours') IS NOT NULL
                    EXEC sp_executesql N'INSERT INTO SmartStudy_SchedulingPreferences (Email, MaxDailyStudyHours, MaxContinuousMinutes, DayStartHour, DayEndHour, SleepHoursPerDay, LunchBreakStart, LunchBreakEnd)
                    SELECT Email, MaxDailyStudyHours, MaxContinuousMinutes, DayStartHour, DayEndHour, SleepHoursPerDay, LunchBreakStart, LunchBreakEnd
                    FROM SmartStudy_Users;'
            END

            -- Drop old scheduling columns from Users (must drop default constraints first)
            DECLARE @sql NVARCHAR(MAX) = '';
            SELECT @sql = @sql + 'ALTER TABLE SmartStudy_Users DROP CONSTRAINT ' + QUOTENAME(dc.name) + '; '
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID('SmartStudy_Users')
              AND c.name IN ('MaxDailyStudyHours','MaxContinuousMinutes','DayStartHour','DayEndHour','SleepHoursPerDay','LunchBreakStart','LunchBreakEnd');
            IF @sql <> '' EXEC sp_executesql @sql;

            IF COL_LENGTH('SmartStudy_Users', 'MaxDailyStudyHours') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN MaxDailyStudyHours;
            IF COL_LENGTH('SmartStudy_Users', 'MaxContinuousMinutes') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN MaxContinuousMinutes;
            IF COL_LENGTH('SmartStudy_Users', 'DayStartHour') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN DayStartHour;
            IF COL_LENGTH('SmartStudy_Users', 'DayEndHour') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN DayEndHour;
            IF COL_LENGTH('SmartStudy_Users', 'SleepHoursPerDay') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN SleepHoursPerDay;
            IF COL_LENGTH('SmartStudy_Users', 'LunchBreakStart') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN LunchBreakStart;
            IF COL_LENGTH('SmartStudy_Users', 'LunchBreakEnd') IS NOT NULL
                ALTER TABLE SmartStudy_Users DROP COLUMN LunchBreakEnd;
        ";
        schedCmd.ExecuteNonQuery();
    }
    // Phase 6+8 migrations: SharedByDefault, Google Calendar fields
    using (var phase2Cmd = conn.CreateCommand())
    {
        phase2Cmd.CommandText = @"
            -- Add SharedByDefault to UserCourses
            IF COL_LENGTH('SmartStudy_UserCourses', 'SharedByDefault') IS NULL
                ALTER TABLE SmartStudy_UserCourses ADD SharedByDefault BIT NOT NULL DEFAULT 0;

            -- Add Google Calendar fields to Users
            IF COL_LENGTH('SmartStudy_Users', 'GoogleCalendarAccessToken') IS NULL
                ALTER TABLE SmartStudy_Users ADD GoogleCalendarAccessToken NVARCHAR(MAX) NULL;
            IF COL_LENGTH('SmartStudy_Users', 'GoogleCalendarRefreshToken') IS NULL
                ALTER TABLE SmartStudy_Users ADD GoogleCalendarRefreshToken NVARCHAR(MAX) NULL;
            IF COL_LENGTH('SmartStudy_Users', 'LastCalendarSync') IS NULL
                ALTER TABLE SmartStudy_Users ADD LastCalendarSync DATETIME2 NULL;

            -- Add Composio connected account ID to Users
            IF COL_LENGTH('SmartStudy_Users', 'ComposioConnectedAccountId') IS NULL
                ALTER TABLE SmartStudy_Users ADD ComposioConnectedAccountId NVARCHAR(255) NULL;
        ";
        phase2Cmd.ExecuteNonQuery();
    }

    // Scheduling redesign migrations: new columns for SchedulingPreferences, Courses, Tasks
    using (var schedRedesignCmd = conn.CreateCommand())
    {
        schedRedesignCmd.CommandText = @"
            -- Add new scheduling preference columns
            IF COL_LENGTH('SmartStudy_SchedulingPreferences', 'BreakDurationMinutes') IS NULL
                ALTER TABLE SmartStudy_SchedulingPreferences ADD BreakDurationMinutes INT NOT NULL DEFAULT 15;
            IF COL_LENGTH('SmartStudy_SchedulingPreferences', 'DefaultTaskEstimatedHours') IS NULL
                ALTER TABLE SmartStudy_SchedulingPreferences ADD DefaultTaskEstimatedHours FLOAT NOT NULL DEFAULT 4.0;
            IF COL_LENGTH('SmartStudy_SchedulingPreferences', 'MaxDailyTotalHours') IS NULL
                ALTER TABLE SmartStudy_SchedulingPreferences ADD MaxDailyTotalHours FLOAT NOT NULL DEFAULT 14.0;
            IF COL_LENGTH('SmartStudy_SchedulingPreferences', 'ExamPrepHoursPerDay') IS NULL
                ALTER TABLE SmartStudy_SchedulingPreferences ADD ExamPrepHoursPerDay FLOAT NOT NULL DEFAULT 5.0;
            IF COL_LENGTH('SmartStudy_SchedulingPreferences', 'ExamPrepDays') IS NULL
                ALTER TABLE SmartStudy_SchedulingPreferences ADD ExamPrepDays INT NOT NULL DEFAULT 3;

            -- Add per-course scheduling overrides
            IF COL_LENGTH('SmartStudy_Courses', 'DefaultTaskEstimatedHours') IS NULL
                ALTER TABLE SmartStudy_Courses ADD DefaultTaskEstimatedHours FLOAT NULL;
            IF COL_LENGTH('SmartStudy_Courses', 'ExamPrepHoursPerDay') IS NULL
                ALTER TABLE SmartStudy_Courses ADD ExamPrepHoursPerDay FLOAT NULL;
            IF COL_LENGTH('SmartStudy_Courses', 'ExamPrepDays') IS NULL
                ALTER TABLE SmartStudy_Courses ADD ExamPrepDays INT NULL;

            -- Add AllowSplitting to Tasks
            IF COL_LENGTH('SmartStudy_Tasks', 'AllowSplitting') IS NULL
                ALTER TABLE SmartStudy_Tasks ADD AllowSplitting BIT NOT NULL DEFAULT 0;

            -- Add IsManuallyPinned to Tasks
            IF COL_LENGTH('SmartStudy_Tasks', 'IsManuallyPinned') IS NULL
                ALTER TABLE SmartStudy_Tasks ADD IsManuallyPinned BIT NOT NULL DEFAULT 0;

            -- Add IsTakingExam to Exams
            IF COL_LENGTH('SmartStudy_Exams', 'IsTakingExam') IS NULL
                ALTER TABLE SmartStudy_Exams ADD IsTakingExam BIT NOT NULL DEFAULT 1;

            -- Add CourseShareApproved to UserCourses
            IF COL_LENGTH('SmartStudy_UserCourses', 'CourseShareApproved') IS NULL
                ALTER TABLE SmartStudy_UserCourses ADD CourseShareApproved BIT NOT NULL DEFAULT 0;

            -- Add RecurrenceEndDate to Events
            IF COL_LENGTH('SmartStudy_Events', 'RecurrenceEndDate') IS NULL
                ALTER TABLE SmartStudy_Events ADD RecurrenceEndDate DATETIME2 NULL;

            -- Add IsManualPriority to Tasks
            IF COL_LENGTH('SmartStudy_Tasks', 'IsManualPriority') IS NULL
                ALTER TABLE SmartStudy_Tasks ADD IsManualPriority BIT NOT NULL DEFAULT 0;
        ";
        schedRedesignCmd.ExecuteNonQuery();
    }

    // Ruppinet integration fields
    using (var ruppinetCmd = conn.CreateCommand())
    {
        ruppinetCmd.CommandText = @"
            IF COL_LENGTH('SmartStudy_Users', 'RuppinetId') IS NULL
                ALTER TABLE SmartStudy_Users ADD RuppinetId NVARCHAR(20) NULL;
            IF COL_LENGTH('SmartStudy_Users', 'RuppinetPassword') IS NULL
                ALTER TABLE SmartStudy_Users ADD RuppinetPassword NVARCHAR(500) NULL;
            IF COL_LENGTH('SmartStudy_Users', 'LastRuppinetSync') IS NULL
                ALTER TABLE SmartStudy_Users ADD LastRuppinetSync DATETIME2 NULL;
        ";
        ruppinetCmd.ExecuteNonQuery();
    }

    // Moodle integration fields
    using (var moodleCmd = conn.CreateCommand())
    {
        moodleCmd.CommandText = @"
            IF COL_LENGTH('SmartStudy_Users', 'MoodleToken') IS NULL
                ALTER TABLE SmartStudy_Users ADD MoodleToken NVARCHAR(500) NULL;
            IF COL_LENGTH('SmartStudy_Users', 'LastMoodleSync') IS NULL
                ALTER TABLE SmartStudy_Users ADD LastMoodleSync DATETIME2 NULL;
            IF COL_LENGTH('SmartStudy_Tasks', 'MoodleId') IS NULL
                ALTER TABLE SmartStudy_Tasks ADD MoodleId NVARCHAR(100) NULL;
        ";
        moodleCmd.ExecuteNonQuery();
    }

    conn.Close();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve frontend static files from Front/ directory (dev only, prod uses separate IIS app)
var frontPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Front"));
if (!Directory.Exists(frontPath))
    frontPath = Path.Combine(builder.Environment.ContentRootPath, "Front");

if (Directory.Exists(frontPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontPath),
        RequestPath = ""
    });

    app.MapGet("/", () => Results.Redirect("Pages/Login.html"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
