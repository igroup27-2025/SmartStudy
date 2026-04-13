-- Migration: bugfix bundle 2026-04-12
-- 1) SS_Tasks_Delete: robust delete for shared / previously-shared-then-cancelled tasks
-- 2) SS_Exams_Delete: clean up linked study tasks + their events before deleting the exam
-- 3) SS_Events_GetAllTypedInRange: also expose SharedStatus so the client can show Pending shared tasks
-- 4) SS_TaskEvents_GetByTaskIdsAndStatuses: include pending-approval statuses so shared
--    re-scheduling does not leave duplicate events behind
-- 5) SS_SharedTasks_GetPartnerTaskId / SS_TaskEvents_SyncSharedMove: keep paired
--    shared-task events in sync when either side drags to a new time

-- =====================================================
-- 1) Tasks delete — handles sub-task events, shared-task copy references
-- =====================================================
IF OBJECT_ID('SS_Tasks_Delete','P') IS NOT NULL DROP PROCEDURE SS_Tasks_Delete;
GO
CREATE PROCEDURE SS_Tasks_Delete
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Collect the task plus all its sub-tasks so every descendant's
    -- TaskEvents are purged before we try to delete the rows themselves
    -- (FK_TaskEvents_Tasks is NO ACTION, so lingering task events would
    -- otherwise block deletion of a task that was ever scheduled).
    DECLARE @TasksToDelete TABLE (TaskId INT PRIMARY KEY);
    INSERT INTO @TasksToDelete (TaskId) VALUES (@TaskId);
    INSERT INTO @TasksToDelete (TaskId)
        SELECT TaskId FROM SmartStudy_Tasks
        WHERE ParentTaskId = @TaskId AND TaskId NOT IN (SELECT TaskId FROM @TasksToDelete);

    -- Delete events backing any of those tasks (TaskEvents cascade with Event row).
    DELETE FROM SmartStudy_Events
    WHERE EventId IN (
        SELECT te.EventId FROM SmartStudy_TaskEvents te
        WHERE te.TaskId IN (SELECT TaskId FROM @TasksToDelete)
    );

    -- A partner's CopyTaskId may point to this task. The column has no FK,
    -- but leaving it pointing at a deleted row corrupts the shared-info view,
    -- so null it out when a referenced copy is being removed.
    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = NULL
    WHERE CopyTaskId IN (SELECT TaskId FROM @TasksToDelete);

    -- Delete sub-tasks first, then the parent task. SharedTasks and
    -- SharedTaskMembers cascade automatically (FK is ON DELETE CASCADE).
    DELETE FROM SmartStudy_Tasks WHERE ParentTaskId = @TaskId;
    DELETE FROM SmartStudy_Tasks WHERE TaskId = @TaskId;
END
GO

-- =====================================================
-- 2) Exam delete — also clean up linked "Study for exam" tasks + their events
-- =====================================================
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
        -- Remove scheduled study blocks for this exam
        DELETE FROM SmartStudy_Events WHERE EventId IN (
            SELECT te.EventId
            FROM SmartStudy_TaskEvents te
            INNER JOIN SmartStudy_Tasks t ON te.TaskId = t.TaskId
            WHERE t.CourseId = @CourseId
              AND t.[Type] = 'Study for exam'
              AND CAST(t.DueDate AS DATE) = CAST(@ExamDate AS DATE)
        );

        -- Remove study tasks tied to this exam
        DELETE FROM SmartStudy_Tasks
        WHERE CourseId = @CourseId
          AND [Type] = 'Study for exam'
          AND CAST(DueDate AS DATE) = CAST(@ExamDate AS DATE);
    END

    DELETE FROM SmartStudy_Exams WHERE ExamId = @ExamId;
END
GO

-- =====================================================
-- 3) Typed events view now surfaces SharedStatus for task events
-- =====================================================
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
        ce.CourseId,
        c.CourseName,
        ce.[Location],
        ce.Duration,
        te.TaskId,
        t.Title AS TaskTitle,
        te.[Priority],
        te.ActualHours,
        te.[Status],
        t.IsManuallyPinned,
        CAST(CASE
            WHEN st_direct.TaskId IS NOT NULL OR st_copy.TaskId IS NOT NULL THEN 1
            ELSE 0
        END AS BIT) AS IsShared,
        COALESCE(st_direct.SharedStatus, st_copy.SharedStatus) AS SharedStatus,
        we.TravelTime,
        we.WorkPlace,
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

-- =====================================================
-- 4) Include NeedReview + PartiallyApproved in shared re-scheduling cleanup
-- =====================================================
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

-- =====================================================
-- 5) Helpers to mirror a dragged shared-task event to the partner
-- =====================================================
IF OBJECT_ID('SS_SharedTasks_GetPartnerTaskId','P') IS NOT NULL DROP PROCEDURE SS_SharedTasks_GetPartnerTaskId;
GO
CREATE PROCEDURE SS_SharedTasks_GetPartnerTaskId
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @PartnerTaskId INT = NULL;

    -- Case A: @TaskId is the creator's task — partner's copy is on a member row
    SELECT TOP 1 @PartnerTaskId = stm.CopyTaskId
    FROM SmartStudy_SharedTasks st
    INNER JOIN SmartStudy_SharedTaskMembers stm
        ON stm.TaskId = st.TaskId
       AND stm.CopyTaskId IS NOT NULL
       AND stm.Email <> st.CreatedByEmail
    WHERE st.TaskId = @TaskId
      AND st.SharedStatus = 'Confirmed';

    -- Case B: @TaskId is a partner's copy — creator's task is the SharedTasks row
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

    -- Locate the partner's event that matches the pre-move time window.
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

-- =====================================================
-- 6) Explicit CopyTaskId lookup for a single member row
-- =====================================================
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

-- =====================================================
-- 7) Deletes a shared-task member's copy task + its events (used when
--    a shared task is cancelled / declined so the copy doesn't linger
--    on the partner's calendar).
-- =====================================================
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

    -- Delete copies' TaskEvents (via Events cascade) then the tasks themselves
    DELETE FROM SmartStudy_Events WHERE EventId IN (
        SELECT te.EventId FROM SmartStudy_TaskEvents te
        WHERE te.TaskId IN (SELECT TaskId FROM @CopyIds)
    );

    DELETE FROM SmartStudy_Tasks
    WHERE TaskId IN (SELECT TaskId FROM @CopyIds);

    -- Clear the now-dangling back-references
    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = NULL
    WHERE TaskId = @TaskId;

    SELECT COUNT(*) AS DeletedCount FROM @CopyIds;
END
GO

-- =====================================================
-- 8) Live DB has FK_TaskEvents_Events (and sibling subtype FKs) configured as
--    NO_ACTION (EF Core default). The earlier delete procs tried to DELETE
--    FROM SmartStudy_Events expecting cascade — which never fires, so shared /
--    previously-shared tasks could never be deleted. The procs below remove
--    the TaskEvents rows BEFORE the Events rows so deletion works regardless
--    of the constraint's referential action.
-- =====================================================
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

    DELETE FROM SmartStudy_TaskEvents WHERE EventId IN (SELECT EventId FROM @EventIds);
    DELETE FROM SmartStudy_Events      WHERE EventId IN (SELECT EventId FROM @EventIds);

    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = NULL
    WHERE CopyTaskId IN (SELECT TaskId FROM @TasksToDelete);

    DELETE FROM SmartStudy_Tasks WHERE ParentTaskId = @TaskId;
    DELETE FROM SmartStudy_Tasks WHERE TaskId = @TaskId;
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
        DELETE FROM SmartStudy_Events      WHERE EventId IN (SELECT EventId FROM @StudyEventIds);
        DELETE FROM SmartStudy_Tasks       WHERE TaskId  IN (SELECT TaskId  FROM @StudyTaskIds);
    END

    DELETE FROM SmartStudy_Exams WHERE ExamId = @ExamId;
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
    DELETE FROM SmartStudy_Events      WHERE EventId IN (SELECT EventId FROM @StudyEventIds);
    DELETE FROM SmartStudy_Tasks       WHERE TaskId  IN (SELECT TaskId  FROM @StudyTaskIds);
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
    DELETE FROM SmartStudy_Events      WHERE EventId IN (SELECT EventId FROM @CopyEventIds);
    DELETE FROM SmartStudy_Tasks       WHERE TaskId  IN (SELECT TaskId  FROM @CopyIds);

    UPDATE SmartStudy_SharedTaskMembers
    SET CopyTaskId = NULL
    WHERE TaskId = @TaskId;

    SELECT COUNT(*) AS DeletedCount FROM @CopyIds;
END
GO
