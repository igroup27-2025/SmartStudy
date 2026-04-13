-- Migration: shared-task approval + calendar "Shared" indicator.
-- Apply on the live database once. No table changes — stored procedures only.

-- 1) Require both users to approve a shared task before removing the Pending tag.
IF OBJECT_ID('SS_TaskEvents_Approve','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_Approve;
GO
CREATE PROCEDURE SS_TaskEvents_Approve
    @TaskId INT,
    @Email NVARCHAR(255),
    @Now DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CreatorTaskId INT = NULL;
    DECLARE @PartnerTaskId INT = NULL;

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
        UPDATE te SET te.Status = 'PartiallyApproved'
        FROM SmartStudy_TaskEvents te
        INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
        WHERE te.TaskId = @TaskId AND t.Email = @Email AND te.Status = 'NeedReview';

        SET @ApprovedCount = @@ROWCOUNT;

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

        IF @PartnerPending = 0 AND @PartnerApproved > 0
        BEGIN
            UPDATE SmartStudy_TaskEvents SET Status = 'Scheduled'
            WHERE TaskId IN (@CreatorTaskId, @PartnerTaskId) AND Status = 'PartiallyApproved';
        END
    END
    ELSE
    BEGIN
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

-- 2) Keep tasks with PartiallyApproved events out of the auto-scheduler.
IF OBJECT_ID('SS_TaskEvents_GetNeedReviewTaskIds','P') IS NOT NULL DROP PROCEDURE SS_TaskEvents_GetNeedReviewTaskIds;
GO
CREATE PROCEDURE SS_TaskEvents_GetNeedReviewTaskIds
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT te.TaskId
    FROM SmartStudy_TaskEvents te
    INNER JOIN SmartStudy_Tasks t ON t.TaskId = te.TaskId
    WHERE t.Email = @Email AND te.Status IN ('NeedReview', 'PartiallyApproved');
END
GO

-- 3) Surface IsShared on calendar events (for both creator and partner copies).
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

-- 4) Same IsShared column on conflict-check SP (MapTypedEvent reuses the shape).
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
