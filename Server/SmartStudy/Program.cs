using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartStudy.Data;
using SmartStudy.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - SQL Server
var connectionString = builder.Configuration.GetConnectionString("SmartStudyDb");
builder.Services.AddDbContext<SmartStudyDbContext>(options =>
    options.UseSqlServer(connectionString));

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

                -- Migrate existing data from Users columns if they exist
                IF COL_LENGTH('SmartStudy_Users', 'MaxDailyStudyHours') IS NOT NULL
                BEGIN
                    INSERT INTO SmartStudy_SchedulingPreferences (Email, MaxDailyStudyHours, MaxContinuousMinutes, DayStartHour, DayEndHour, SleepHoursPerDay, LunchBreakStart, LunchBreakEnd)
                    SELECT Email, MaxDailyStudyHours, MaxContinuousMinutes, DayStartHour, DayEndHour, SleepHoursPerDay, LunchBreakStart, LunchBreakEnd
                    FROM SmartStudy_Users;
                END
            END

            -- Drop old scheduling columns from Users if they exist
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
    conn.Close();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve frontend static files from Front/ directory
var frontPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Front"));
if (Directory.Exists(frontPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontPath),
        RequestPath = ""
    });

    // Redirect root to login page
    app.MapGet("/", () => Results.Redirect("/Pages/Login.html"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
