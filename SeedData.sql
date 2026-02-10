-- =====================================================
-- SmartStudy Seed Data for SQL Server
-- Run AFTER the schema (SQL file) has been executed
-- =====================================================

-- ===== 1) USERS =====
INSERT INTO SmartStudy_Users (Email, FirstName, LastName, [Password]) VALUES
  (N'demo@smartstudy.com',  N'Or',    N'Cohen', N'LqoUjFlaFomQR6fYJyBbX3ryQL1et+LkFw4l8DSn5U8='),
  (N'yuval@smartstudy.com', N'Yuval', N'Rotel', N'EuXx8dfFRPvMwWcUgRK8pUYj03KtYiMtXt24UbGT3xo=');
-- Passwords: demo = Demo123, yuval = Test123 (SHA256 + salt)

-- ===== 2) NOTIFICATION SETTINGS =====
INSERT INTO SmartStudy_NotificationSettings (Email, Notify_before_task, Daily_morning_summary, Weekly_plan_reminder, Enable_push_notification) VALUES
  (N'demo@smartstudy.com',  1, 1, 0, 0),
  (N'yuval@smartstudy.com', 1, 0, 0, 0);

-- ===== 3) INSTRUCTORS =====
SET IDENTITY_INSERT SmartStudy_Instructors ON;
INSERT INTO SmartStudy_Instructors (InstructorId, InstructorName) VALUES
  (1, N'Dr. Sarah Miller'),
  (2, N'Prof. David Chen'),
  (3, N'Dr. Rachel Green');
SET IDENTITY_INSERT SmartStudy_Instructors OFF;

-- ===== 4) COURSES =====
INSERT INTO SmartStudy_Courses (CourseId, CourseName, WeeklyHours, Credits, Semester, InstructorId) VALUES
  (1, N'Introduction to Computer Science', 4.0, 3.0, N'2025B', 1),
  (2, N'Data Structures & Algorithms',     5.0, 4.0, N'2025B', 2),
  (3, N'Linear Algebra',                   3.0, 3.0, N'2025B', 3),
  (4, N'Probability & Statistics',          3.0, 3.0, N'2025B', 1),
  (5, N'Web Development',                  4.0, 3.0, N'2025B', 2);

-- ===== 5) USER-COURSE ENROLLMENTS =====
-- Or is enrolled in all 5 courses
INSERT INTO SmartStudy_UserCourses (Email, CourseId) VALUES
  (N'demo@smartstudy.com', 1),
  (N'demo@smartstudy.com', 2),
  (N'demo@smartstudy.com', 3),
  (N'demo@smartstudy.com', 4),
  (N'demo@smartstudy.com', 5);

-- Yuval is enrolled in 3 courses
INSERT INTO SmartStudy_UserCourses (Email, CourseId) VALUES
  (N'yuval@smartstudy.com', 1),
  (N'yuval@smartstudy.com', 2),
  (N'yuval@smartstudy.com', 3);

-- ===== 6) EXAMS =====
SET IDENTITY_INSERT SmartStudy_Exams ON;
INSERT INTO SmartStudy_Exams (ExamId, CourseId, [Date], [Time], [Session], Duration) VALUES
  (1, 1, '2026-02-20', '09:00:00', N'A', 120),   -- CS Intro Moed A
  (2, 1, '2026-03-15', '09:00:00', N'B', 180),   -- CS Intro Moed B
  (3, 2, '2026-02-25', '14:00:00', N'A', 150),   -- Data Structures Moed A
  (4, 3, '2026-03-10', '10:00:00', N'A', 120),   -- Linear Algebra Moed A
  (5, 4, '2026-02-28', '11:00:00', N'A',  90),   -- Probability Moed A
  (6, 5, '2026-03-20', '14:00:00', N'B', 120);   -- Web Dev Moed B
SET IDENTITY_INSERT SmartStudy_Exams OFF;

-- ===== 7) TASKS =====
SET IDENTITY_INSERT SmartStudy_Tasks ON;
INSERT INTO SmartStudy_Tasks (TaskId, CourseId, Title, [Type], EstimatedHours, DueDate, IsCompleted, [Priority], Email) VALUES
  -- CS Intro tasks (Or)
  ( 1, 1, N'Homework 1 - Variables & Loops',      N'Homework',   3.00, '2026-02-05', 1, N'Medium', N'demo@smartstudy.com'),
  ( 2, 1, N'Homework 2 - Functions & Arrays',      N'Homework',   4.00, '2026-02-15', 0, N'High',   N'demo@smartstudy.com'),
  ( 3, 1, N'Project Proposal',                     N'Project',    6.00, '2026-02-18', 0, N'High',   N'demo@smartstudy.com'),

  -- Data Structures tasks (Or)
  ( 4, 2, N'Lab 1 - Linked Lists',                 N'Lab',        2.00, '2026-02-03', 1, N'Medium', N'demo@smartstudy.com'),
  ( 5, 2, N'Lab 2 - Binary Trees',                 N'Lab',        3.00, '2026-02-14', 0, N'High',   N'demo@smartstudy.com'),
  ( 6, 2, N'Assignment 1 - Sorting Algorithms',    N'Assignment', 8.00, '2026-02-22', 0, N'High',   N'demo@smartstudy.com'),

  -- Linear Algebra tasks (Or)
  ( 7, 3, N'Problem Set 1 - Matrices',             N'Homework',   2.00, '2026-02-01', 1, N'Low',    N'demo@smartstudy.com'),
  ( 8, 3, N'Problem Set 2 - Eigenvalues',          N'Homework',   4.00, '2026-02-16', 0, N'Medium', N'demo@smartstudy.com'),

  -- Web Dev tasks (Or)
  ( 9, 5, N'HTML/CSS Assignment',                  N'Assignment', 3.00, '2026-02-02', 1, N'Low',    N'demo@smartstudy.com'),
  (10, 5, N'JavaScript Project',                   N'Project',   10.00, '2026-02-20', 0, N'High',   N'demo@smartstudy.com'),
  (11, 5, N'Final Project - Full Stack App',       N'Project',   20.00, '2026-03-15', 0, N'High',   N'demo@smartstudy.com'),

  -- Probability tasks (Or)
  (12, 4, N'Exercise 1 - Basic Probability',       N'Homework',   2.00, '2026-02-12', 0, N'Medium', N'demo@smartstudy.com'),
  (13, 4, N'Exercise 2 - Distributions',           N'Homework',   3.00, '2026-02-19', 0, N'Medium', N'demo@smartstudy.com'),

  -- Yuval's tasks
  (14, 1, N'Homework 1 - Variables',               N'Homework',   3.00, '2026-02-10', 0, N'High',   N'yuval@smartstudy.com'),
  (15, 2, N'Lab 1 - Stacks',                       N'Lab',        2.00, '2026-02-12', 0, N'Medium', N'yuval@smartstudy.com');
SET IDENTITY_INSERT SmartStudy_Tasks OFF;

-- ===== 8) EVENTS (base table) =====
-- We use the current week's Monday as reference for recurring events.
-- Adjust dates as needed for your testing window.
DECLARE @Monday DATETIME2 = DATEADD(DAY, 1 - DATEPART(WEEKDAY, GETDATE()), CAST(GETDATE() AS DATE));

SET IDENTITY_INSERT SmartStudy_Events ON;

-- Class events (EventId 1–9)
INSERT INTO SmartStudy_Events (EventId, Email, [From], [To], Recurring) VALUES
  ( 1, N'demo@smartstudy.com', DATEADD(HOUR,  9, @Monday),                          DATEADD(MINUTE, 90, DATEADD(HOUR,  9, @Monday)),                          1),  -- Mon 09:00-10:30 CS
  ( 2, N'demo@smartstudy.com', DATEADD(HOUR,  9, DATEADD(DAY, 2, @Monday)),         DATEADD(MINUTE, 90, DATEADD(HOUR,  9, DATEADD(DAY, 2, @Monday))),         1),  -- Wed 09:00-10:30 CS
  ( 3, N'demo@smartstudy.com', DATEADD(HOUR, 11, DATEADD(DAY, 1, @Monday)),         DATEADD(MINUTE, 90, DATEADD(HOUR, 11, DATEADD(DAY, 1, @Monday))),         1),  -- Tue 11:00-12:30 DS
  ( 4, N'demo@smartstudy.com', DATEADD(HOUR, 11, DATEADD(DAY, 3, @Monday)),         DATEADD(MINUTE, 90, DATEADD(HOUR, 11, DATEADD(DAY, 3, @Monday))),         1),  -- Thu 11:00-12:30 DS
  ( 5, N'demo@smartstudy.com', DATEADD(HOUR, 14, @Monday),                          DATEADD(MINUTE, 90, DATEADD(HOUR, 14, @Monday)),                          1),  -- Mon 14:00-15:30 LA
  ( 6, N'demo@smartstudy.com', DATEADD(HOUR, 14, DATEADD(DAY, 2, @Monday)),         DATEADD(MINUTE, 90, DATEADD(HOUR, 14, DATEADD(DAY, 2, @Monday))),         1),  -- Wed 14:00-15:30 LA
  ( 7, N'demo@smartstudy.com', DATEADD(HOUR, 10, DATEADD(DAY, 4, @Monday)),         DATEADD(HOUR,  13, DATEADD(DAY, 4, @Monday)),                             1),  -- Fri 10:00-13:00 Web
  ( 8, N'demo@smartstudy.com', DATEADD(HOUR, 14, DATEADD(DAY, 1, @Monday)),         DATEADD(MINUTE, 90, DATEADD(HOUR, 14, DATEADD(DAY, 1, @Monday))),         1),  -- Tue 14:00-15:30 Prob
  ( 9, N'demo@smartstudy.com', DATEADD(HOUR, 14, DATEADD(DAY, 3, @Monday)),         DATEADD(MINUTE, 90, DATEADD(HOUR, 14, DATEADD(DAY, 3, @Monday))),         1); -- Thu 14:00-15:30 Prob

-- Work events (EventId 10–11)
INSERT INTO SmartStudy_Events (EventId, Email, [From], [To], Recurring) VALUES
  (10, N'demo@smartstudy.com', DATEADD(HOUR, 9, DATEADD(DAY, 5, @Monday)),          DATEADD(HOUR, 17, DATEADD(DAY, 5, @Monday)),           1),  -- Sat 09:00-17:00
  (11, N'demo@smartstudy.com', DATEADD(HOUR, 9, DATEADD(DAY, 12, @Monday)),         DATEADD(HOUR, 17, DATEADD(DAY, 12, @Monday)),          1); -- Next Sat 09:00-17:00

-- Personal events (EventId 12–16)
INSERT INTO SmartStudy_Events (EventId, Email, [From], [To], Recurring) VALUES
  (12, N'demo@smartstudy.com', DATEADD(HOUR, 7, @Monday),                           DATEADD(HOUR,  8, @Monday),                            1),  -- Mon 07:00-08:00 Gym
  (13, N'demo@smartstudy.com', DATEADD(HOUR, 7, DATEADD(DAY, 2, @Monday)),          DATEADD(HOUR,  8, DATEADD(DAY, 2, @Monday)),           1),  -- Wed 07:00-08:00 Gym
  (14, N'demo@smartstudy.com', DATEADD(HOUR, 7, DATEADD(DAY, 4, @Monday)),          DATEADD(HOUR,  8, DATEADD(DAY, 4, @Monday)),           1),  -- Fri 07:00-08:00 Gym
  (15, N'demo@smartstudy.com', DATEADD(HOUR, 18, DATEADD(DAY, 3, @Monday)),         DATEADD(HOUR, 20, DATEADD(DAY, 3, @Monday)),           1),  -- Thu 18:00-20:00 Study group
  (16, N'demo@smartstudy.com', DATEADD(HOUR, 10, DATEADD(DAY, 6, @Monday)),         DATEADD(HOUR, 12, DATEADD(DAY, 6, @Monday)),           0); -- Sun 10:00-12:00 Doctor

SET IDENTITY_INSERT SmartStudy_Events OFF;

-- ===== 9) CLASS EVENTS (subtype) =====
INSERT INTO SmartStudy_ClassEvents (EventId, CourseId, Location, Duration) VALUES
  (1, 1, N'Room 101', 1.50),   -- CS Mon
  (2, 1, N'Room 101', 1.50),   -- CS Wed
  (3, 2, N'Room 205', 1.50),   -- DS Tue
  (4, 2, N'Room 205', 1.50),   -- DS Thu
  (5, 3, N'Room 303', 1.50),   -- LA Mon
  (6, 3, N'Room 303', 1.50),   -- LA Wed
  (7, 5, N'Lab 401',  3.00),   -- Web Fri
  (8, 4, N'Room 102', 1.50),   -- Prob Tue
  (9, 4, N'Room 102', 1.50);   -- Prob Thu

-- ===== 10) WORK EVENTS (subtype) =====
INSERT INTO SmartStudy_WorkEvents (EventId, TravelTime, WorkPlace) VALUES
  (10, 30, N'Tech Startup Inc.'),
  (11, 30, N'Tech Startup Inc.');

-- ===== 11) PERSONAL EVENTS (subtype) =====
INSERT INTO SmartStudy_PersonalEvents (EventId, [Type], [Description]) VALUES
  (12, N'Exercise', N'Morning gym session'),
  (13, N'Exercise', N'Morning gym session'),
  (14, N'Exercise', N'Morning gym session'),
  (15, N'Social',   N'Study group meeting'),
  (16, N'Errand',   N'Doctor appointment');

-- ===== 12) TASK EVENTS (linking tasks to calendar) =====
-- Create calendar events for some upcoming incomplete tasks
DECLARE @TaskMon DATETIME2 = DATEADD(DAY, 2, @Monday); -- Wednesday

SET IDENTITY_INSERT SmartStudy_Events ON;
INSERT INTO SmartStudy_Events (EventId, Email, [From], [To], Recurring) VALUES
  (17, N'demo@smartstudy.com', DATEADD(HOUR, 16, @Monday),                  DATEADD(HOUR, 18, @Monday),                  0),  -- Mon 16-18: HW2 work
  (18, N'demo@smartstudy.com', DATEADD(HOUR, 16, DATEADD(DAY, 1, @Monday)), DATEADD(HOUR, 19, DATEADD(DAY, 1, @Monday)), 0),  -- Tue 16-19: Binary Trees
  (19, N'demo@smartstudy.com', DATEADD(HOUR, 16, DATEADD(DAY, 2, @Monday)), DATEADD(HOUR, 19, DATEADD(DAY, 2, @Monday)), 0);  -- Wed 16-19: Sorting Alg
SET IDENTITY_INSERT SmartStudy_Events OFF;

INSERT INTO SmartStudy_TaskEvents (EventId, TaskId, [Priority], ActualHours, [Status]) VALUES
  (17,  2, N'High',   NULL, N'In Progress'),   -- HW2 Functions & Arrays
  (18,  5, N'High',   NULL, N'Not Started'),   -- Lab 2 Binary Trees
  (19,  6, N'High',   NULL, N'Not Started');   -- Assignment 1 Sorting

-- ===== 13) EXTRA USERS FOR CONNECTIONS =====
INSERT INTO SmartStudy_Users (Email, FirstName, LastName, [Password]) VALUES
  (N'sarah.cohen@uni.ac.il', N'Sarah', N'Cohen', N'LqoUjFlaFomQR6fYJyBbX3ryQL1et+LkFw4l8DSn5U8='),
  (N'david.levi@uni.ac.il',  N'David', N'Levi',  N'LqoUjFlaFomQR6fYJyBbX3ryQL1et+LkFw4l8DSn5U8='),
  (N'maya.alon@uni.ac.il',   N'Maya',  N'Alon',  N'LqoUjFlaFomQR6fYJyBbX3ryQL1et+LkFw4l8DSn5U8=');

INSERT INTO SmartStudy_NotificationSettings (Email, Notify_before_task, Daily_morning_summary, Weekly_plan_reminder, Enable_push_notification) VALUES
  (N'sarah.cohen@uni.ac.il', 1, 0, 0, 0),
  (N'david.levi@uni.ac.il',  1, 0, 0, 0),
  (N'maya.alon@uni.ac.il',   1, 0, 0, 0);

-- ===== 14) STUDY CONNECTIONS =====
SET IDENTITY_INSERT SmartStudy_StudyConnections ON;
INSERT INTO SmartStudy_StudyConnections (ConnectionId, RequesterEmail, ReceiverEmail, [Status], CreatedAt, AcceptedAt) VALUES
  (1, N'demo@smartstudy.com', N'sarah.cohen@uni.ac.il', N'Accepted', '2026-01-15', '2026-01-15'),
  (2, N'demo@smartstudy.com', N'david.levi@uni.ac.il',  N'Accepted', '2026-01-22', '2026-01-22'),
  (3, N'maya.alon@uni.ac.il', N'demo@smartstudy.com',   N'Pending',  '2026-02-05', NULL);
SET IDENTITY_INSERT SmartStudy_StudyConnections OFF;

PRINT 'Seed data inserted successfully!';
