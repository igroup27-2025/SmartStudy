-- Migration: per-exam study time overrides (2026-04-14)
-- Adds exam-level ExamPrepHoursPerDay / ExamPrepDays so users can override the
-- course-level defaults directly from the Exams page. Scheduling resolves in
-- the order: exam-level -> course-level -> user preferences.

IF COL_LENGTH('SmartStudy_Exams', 'ExamPrepHoursPerDay') IS NULL
    ALTER TABLE SmartStudy_Exams ADD ExamPrepHoursPerDay FLOAT NULL;
GO

IF COL_LENGTH('SmartStudy_Exams', 'ExamPrepDays') IS NULL
    ALTER TABLE SmartStudy_Exams ADD ExamPrepDays INT NULL;
GO

IF OBJECT_ID('SS_Exams_GetByUser','P') IS NOT NULL DROP PROCEDURE SS_Exams_GetByUser;
GO
CREATE PROCEDURE SS_Exams_GetByUser
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ExamId, e.CourseId, c.CourseName, e.[Date], e.[Time], e.[Session], e.Duration, e.IsTakingExam,
           e.ExamPrepHoursPerDay, e.ExamPrepDays
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
           e.ExamPrepHoursPerDay, e.ExamPrepDays
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
    @IsTakingExam BIT = 1,
    @ExamPrepHoursPerDay FLOAT = NULL,
    @ExamPrepDays INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SmartStudy_Exams (CourseId, [Date], [Time], [Session], Duration, IsTakingExam, ExamPrepHoursPerDay, ExamPrepDays)
    VALUES (@CourseId, @Date, @Time, @Session, @Duration, @IsTakingExam, @ExamPrepHoursPerDay, @ExamPrepDays);
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
    @Duration INT = NULL,
    @ExamPrepHoursPerDay FLOAT = NULL,
    @ExamPrepDays INT = NULL,
    @ClearExamPrep BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE SmartStudy_Exams
    SET CourseId = ISNULL(@CourseId, CourseId),
        [Date] = ISNULL(@Date, [Date]),
        [Time] = ISNULL(@Time, [Time]),
        [Session] = ISNULL(@Session, [Session]),
        Duration = ISNULL(@Duration, Duration),
        ExamPrepHoursPerDay = CASE WHEN @ClearExamPrep = 1 THEN @ExamPrepHoursPerDay ELSE ISNULL(@ExamPrepHoursPerDay, ExamPrepHoursPerDay) END,
        ExamPrepDays = CASE WHEN @ClearExamPrep = 1 THEN @ExamPrepDays ELSE ISNULL(@ExamPrepDays, ExamPrepDays) END
    WHERE ExamId = @ExamId;
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
           c.ExamPrepHoursPerDay AS CourseExamPrepHoursPerDay, c.ExamPrepDays AS CourseExamPrepDays,
           e.ExamPrepHoursPerDay, e.ExamPrepDays
    FROM SmartStudy_Exams e
    INNER JOIN SmartStudy_Courses c ON c.CourseId = e.CourseId
    INNER JOIN SmartStudy_UserCourses uc ON uc.CourseId = e.CourseId AND uc.Email = @Email
    WHERE e.Date >= @RangeStart AND e.Date <= @RangeEnd AND e.IsTakingExam = 1;
END
GO
