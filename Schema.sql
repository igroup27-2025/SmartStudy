-- =====================================================
-- SmartStudy Database Schema for SQL Server
-- 16 tables with SmartStudy_ prefix
-- Run this BEFORE SeedData.sql
-- =====================================================

-- ===== DROP TABLES (reverse dependency order) =====
IF OBJECT_ID('SmartStudy_SharedTaskMembers','U') IS NOT NULL DROP TABLE SmartStudy_SharedTaskMembers;
IF OBJECT_ID('SmartStudy_SharedTasks','U')       IS NOT NULL DROP TABLE SmartStudy_SharedTasks;
IF OBJECT_ID('SmartStudy_Friendships','U')       IS NOT NULL DROP TABLE SmartStudy_Friendships;
IF OBJECT_ID('SmartStudy_FriendRequests','U')    IS NOT NULL DROP TABLE SmartStudy_FriendRequests;
IF OBJECT_ID('SmartStudy_StudyConnections','U')  IS NOT NULL DROP TABLE SmartStudy_StudyConnections;
IF OBJECT_ID('SmartStudy_TaskEvents','U')       IS NOT NULL DROP TABLE SmartStudy_TaskEvents;
IF OBJECT_ID('SmartStudy_ClassEvents','U')      IS NOT NULL DROP TABLE SmartStudy_ClassEvents;
IF OBJECT_ID('SmartStudy_WorkEvents','U')       IS NOT NULL DROP TABLE SmartStudy_WorkEvents;
IF OBJECT_ID('SmartStudy_PersonalEvents','U')   IS NOT NULL DROP TABLE SmartStudy_PersonalEvents;
IF OBJECT_ID('SmartStudy_Events','U')           IS NOT NULL DROP TABLE SmartStudy_Events;
IF OBJECT_ID('SmartStudy_Exams','U')            IS NOT NULL DROP TABLE SmartStudy_Exams;
IF OBJECT_ID('SmartStudy_Tasks','U')            IS NOT NULL DROP TABLE SmartStudy_Tasks;
IF OBJECT_ID('SmartStudy_UserCourses','U')      IS NOT NULL DROP TABLE SmartStudy_UserCourses;
IF OBJECT_ID('SmartStudy_Courses','U')          IS NOT NULL DROP TABLE SmartStudy_Courses;
IF OBJECT_ID('SmartStudy_NotificationSettings','U') IS NOT NULL DROP TABLE SmartStudy_NotificationSettings;
IF OBJECT_ID('SmartStudy_Users','U')            IS NOT NULL DROP TABLE SmartStudy_Users;
IF OBJECT_ID('SmartStudy_Instructors','U')      IS NOT NULL DROP TABLE SmartStudy_Instructors;
GO

-- =====================================================
-- 1) USERS
-- PK is Email (not surrogate int)
-- =====================================================
CREATE TABLE SmartStudy_Users (
    Email       NVARCHAR(255)   NOT NULL,
    FirstName   NVARCHAR(100)   NOT NULL,
    LastName    NVARCHAR(100)   NOT NULL,
    [Password]  NVARCHAR(255)   NOT NULL,
    CONSTRAINT PK_SmartStudy_Users PRIMARY KEY (Email)
);
GO

-- =====================================================
-- 2) NOTIFICATION SETTINGS
-- 1:1 with Users (same PK)
-- =====================================================
CREATE TABLE SmartStudy_NotificationSettings (
    Email                    NVARCHAR(255) NOT NULL,
    Notify_before_task       BIT           NOT NULL DEFAULT 0,
    Daily_morning_summary    BIT           NOT NULL DEFAULT 0,
    Weekly_plan_reminder     BIT           NOT NULL DEFAULT 0,
    Enable_push_notification BIT           NOT NULL DEFAULT 0,
    CONSTRAINT PK_SmartStudy_NotificationSettings PRIMARY KEY (Email),
    CONSTRAINT FK_NotificationSettings_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
);
GO

-- =====================================================
-- 3) INSTRUCTORS
-- =====================================================
CREATE TABLE SmartStudy_Instructors (
    InstructorId   INT            IDENTITY(1,1) NOT NULL,
    InstructorName NVARCHAR(200)  NOT NULL,
    CONSTRAINT PK_SmartStudy_Instructors PRIMARY KEY (InstructorId)
);
GO

-- =====================================================
-- 4) COURSES
-- CourseId is manually assigned (not identity)
-- =====================================================
CREATE TABLE SmartStudy_Courses (
    CourseId     INT            NOT NULL,
    CourseName   NVARCHAR(200)  NOT NULL,
    WeeklyHours  DECIMAL(4,1)   NULL,
    Credits      DECIMAL(4,1)   NULL,
    Semester     NVARCHAR(50)   NULL,
    InstructorId INT            NULL,
    CONSTRAINT PK_SmartStudy_Courses PRIMARY KEY (CourseId),
    CONSTRAINT FK_Courses_Instructors FOREIGN KEY (InstructorId)
        REFERENCES SmartStudy_Instructors(InstructorId) ON DELETE SET NULL
);
GO

-- =====================================================
-- 5) USER-COURSES (junction table, N:N enrollment)
-- Composite PK (Email, CourseId)
-- =====================================================
CREATE TABLE SmartStudy_UserCourses (
    Email    NVARCHAR(255) NOT NULL,
    CourseId INT           NOT NULL,
    CONSTRAINT PK_SmartStudy_UserCourses PRIMARY KEY (Email, CourseId),
    CONSTRAINT FK_UserCourses_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE,
    CONSTRAINT FK_UserCourses_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 6) EXAMS
-- =====================================================
CREATE TABLE SmartStudy_Exams (
    ExamId    INT          IDENTITY(1,1) NOT NULL,
    CourseId  INT          NOT NULL,
    [Date]    DATETIME2    NOT NULL,
    [Time]    TIME         NOT NULL,
    [Session] NVARCHAR(10) NOT NULL,
    Duration  INT          NULL,
    CONSTRAINT PK_SmartStudy_Exams PRIMARY KEY (ExamId),
    CONSTRAINT FK_Exams_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE
);
GO

-- =====================================================
-- 7) TASKS
-- =====================================================
CREATE TABLE SmartStudy_Tasks (
    TaskId         INT            IDENTITY(1,1) NOT NULL,
    CourseId       INT            NOT NULL,
    Email          NVARCHAR(255)  NOT NULL,
    Title          NVARCHAR(200)  NOT NULL,
    [Type]         NVARCHAR(50)   NOT NULL,
    EstimatedHours DECIMAL(5,2)   NULL,
    DueDate        DATETIME2      NULL,
    IsCompleted    BIT            NOT NULL DEFAULT 0,
    [Priority]     NVARCHAR(20)   NULL,
    CONSTRAINT PK_SmartStudy_Tasks PRIMARY KEY (TaskId),
    CONSTRAINT FK_Tasks_Courses FOREIGN KEY (CourseId)
        REFERENCES SmartStudy_Courses(CourseId) ON DELETE CASCADE,
    CONSTRAINT FK_Tasks_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION
);
GO

-- =====================================================
-- 8) EVENTS (base table — TPT inheritance)
-- Four subtypes: ClassEvents, TaskEvents, WorkEvents, PersonalEvents
-- =====================================================
CREATE TABLE SmartStudy_Events (
    EventId   INT            IDENTITY(1,1) NOT NULL,
    Email     NVARCHAR(255)  NOT NULL,
    [From]    DATETIME2      NOT NULL,
    [To]      DATETIME2      NOT NULL,
    Recurring BIT            NOT NULL DEFAULT 0,
    RecurrenceEndDate DATETIME2 NULL,
    CONSTRAINT PK_SmartStudy_Events PRIMARY KEY (EventId),
    CONSTRAINT FK_Events_Users FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE CASCADE
);
GO

-- =====================================================
-- 9) CLASS EVENTS (TPT subtype)
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
-- 10) TASK EVENTS (TPT subtype)
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
-- 11) WORK EVENTS (TPT subtype)
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
-- 12) PERSONAL EVENTS (TPT subtype)
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
-- 13) FRIEND REQUESTS (invitation lifecycle)
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
-- 14) FRIENDSHIPS (confirmed friends)
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
-- 15) SHARED TASKS (1:1 with Tasks)
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
-- 16) SHARED TASK MEMBERS
-- Composite PK (TaskId, Email)
-- =====================================================
CREATE TABLE SmartStudy_SharedTaskMembers (
    TaskId         INT           NOT NULL,
    Email          NVARCHAR(255) NOT NULL,
    ResponseStatus NVARCHAR(20)  NOT NULL DEFAULT N'Pending',
    RespondedAt    DATETIME2     NULL,
    CONSTRAINT PK_SmartStudy_SharedTaskMembers PRIMARY KEY (TaskId, Email),
    CONSTRAINT FK_SharedTaskMembers_SharedTask FOREIGN KEY (TaskId)
        REFERENCES SmartStudy_SharedTasks(TaskId) ON DELETE CASCADE,
    CONSTRAINT FK_SharedTaskMembers_User FOREIGN KEY (Email)
        REFERENCES SmartStudy_Users(Email) ON DELETE NO ACTION
);
GO

-- =====================================================
-- INDEXES
-- =====================================================
CREATE NONCLUSTERED INDEX IX_Events_Email_From ON SmartStudy_Events(Email, [From]);
CREATE NONCLUSTERED INDEX IX_Tasks_CourseId_DueDate ON SmartStudy_Tasks(CourseId, DueDate);
CREATE NONCLUSTERED INDEX IX_Tasks_Email ON SmartStudy_Tasks(Email);
CREATE NONCLUSTERED INDEX IX_Exams_CourseId_Date ON SmartStudy_Exams(CourseId, [Date]);
CREATE NONCLUSTERED INDEX IX_TaskEvents_TaskId ON SmartStudy_TaskEvents(TaskId);
CREATE NONCLUSTERED INDEX IX_ClassEvents_CourseId ON SmartStudy_ClassEvents(CourseId);
CREATE NONCLUSTERED INDEX IX_UserCourses_CourseId ON SmartStudy_UserCourses(CourseId);
CREATE NONCLUSTERED INDEX IX_Courses_InstructorId ON SmartStudy_Courses(InstructorId);
CREATE UNIQUE NONCLUSTERED INDEX IX_FriendRequests_Pending ON SmartStudy_FriendRequests(RequesterEmail, AddresseeEmail) WHERE [Status] = 'Pending';
CREATE NONCLUSTERED INDEX IX_FriendRequests_Addressee ON SmartStudy_FriendRequests(AddresseeEmail);
CREATE UNIQUE NONCLUSTERED INDEX IX_Friendships_Pair ON SmartStudy_Friendships(Email1, Email2);
CREATE NONCLUSTERED INDEX IX_SharedTaskMembers_Email ON SmartStudy_SharedTaskMembers(Email);
GO

PRINT 'SmartStudy schema created successfully (16 tables).';
