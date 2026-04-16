-- Migration: collapse exam-level prep into single course-level setting (2026-04-14b)
-- Per-exam ExamPrepHoursPerDay / ExamPrepDays were intended as overrides but the
-- product decision is to keep one shared value per course. The exam GET SPs now
-- surface the course's value under the same column names so existing readers
-- keep working without conditional logic.

-- Drop the per-exam columns. Safe because nothing in production has shipped
-- using them yet (added earlier today, no UI surfaced overrides).
IF COL_LENGTH('SmartStudy_Exams', 'ExamPrepHoursPerDay') IS NOT NULL
    ALTER TABLE SmartStudy_Exams DROP COLUMN ExamPrepHoursPerDay;
GO

IF COL_LENGTH('SmartStudy_Exams', 'ExamPrepDays') IS NOT NULL
    ALTER TABLE SmartStudy_Exams DROP COLUMN ExamPrepDays;
GO

-- GetByUser / GetById expose the course's value under the ExamPrep* alias so the
-- exam form reads it directly from /api/exams without a separate course fetch.
IF OBJECT_ID('SS_Exams_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Exams_GetByUser;
GO
CREATE PROCEDURE SS_Exams_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.CourseId, c.CourseName, e.[Date], e.[Time], e.[Session], e.Duration, e.IsTakingExam,
           c.ExamPrepHoursPerDay, c.ExamPrepDays
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
    SELECT e.ExamId, e.CourseId, c.CourseName, e.[Date], e.[Time], e.[Session], e.Duration, e.IsTakingExam,
           c.ExamPrepHoursPerDay, c.ExamPrepDays
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_Courses c ON e.CourseId = c.CourseId
    INNER JOIN SmartStudy_UserCourses uc ON e.CourseId = uc.CourseId AND uc.Email = @Email
    WHERE e.ExamId = @ExamId;
END
GO

-- Restore Create/Update without prep params; controller routes prep changes to
-- SS_Courses_Update so they propagate to every exam in that course at once.
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

-- Scheduling SP no longer needs per-exam prep columns.
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
