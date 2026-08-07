-- ========================================================================
-- SCRIPT v13: STUDENT ATTENDANCE + STUDENT QR CODES
-- ========================================================================
-- Adds:
--   1. Users.QrCodeToken            - unique token encoded in each student's QR
--   2. AttendanceSession            - a window during which a teacher scans QR codes
--   3. AttendanceRecord             - one row per scanned student within a session
--
-- AttendanceType:  0 = Class, 1 = Subject, 2 = SubTopic
-- Session Status:  0 = Open, 1 = Closed, 2 = Cancelled
-- ========================================================================

-- ------------------------------------------------------------------------
-- 1. Users.QrCodeToken
-- ------------------------------------------------------------------------
IF COL_LENGTH('Users', 'QrCodeToken') IS NULL
BEGIN
    ALTER TABLE Users ADD QrCodeToken NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Users_QrCodeToken' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE UNIQUE INDEX UQ_Users_QrCodeToken ON Users(QrCodeToken) WHERE QrCodeToken IS NOT NULL;
END
GO

-- ------------------------------------------------------------------------
-- 2. AttendanceSession
-- ------------------------------------------------------------------------
IF OBJECT_ID('AttendanceSession', 'U') IS NULL
BEGIN
    CREATE TABLE AttendanceSession (
        Id                 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CreationDate       NVARCHAR(19)     NOT NULL,
        ModifiedDate       NVARCHAR(19)     NOT NULL,
        SchoolId           UNIQUEIDENTIFIER NOT NULL,
        TeacherId          UNIQUEIDENTIFIER NOT NULL,
        AttendanceType     INT              NOT NULL,
        ClassroomId        UNIQUEIDENTIFIER NULL,
        SubjectId          UNIQUEIDENTIFIER NULL,
        SubTopicId         UNIQUEIDENTIFIER NULL,
        ClassPreparationId UNIQUEIDENTIFIER NULL,
        [Status]           INT              NOT NULL DEFAULT 0,
        StartedAt          NVARCHAR(19)     NOT NULL,
        EndedAt            NVARCHAR(19)     NULL,
        CreatedBy          UNIQUEIDENTIFIER NOT NULL,
        IsActive           BIT              NOT NULL DEFAULT 1,
        CONSTRAINT PK_AttendanceSession PRIMARY KEY (Id)
    );

    CREATE INDEX IX_AttendanceSession_School_Status ON AttendanceSession(SchoolId, [Status]);
    CREATE INDEX IX_AttendanceSession_Teacher      ON AttendanceSession(TeacherId);
    CREATE INDEX IX_AttendanceSession_Classroom    ON AttendanceSession(ClassroomId);
    CREATE INDEX IX_AttendanceSession_Subject      ON AttendanceSession(SubjectId);
    CREATE INDEX IX_AttendanceSession_SubTopic     ON AttendanceSession(SubTopicId);
END
GO

-- ------------------------------------------------------------------------
-- 3. AttendanceRecord
-- ------------------------------------------------------------------------
IF OBJECT_ID('AttendanceRecord', 'U') IS NULL
BEGIN
    CREATE TABLE AttendanceRecord (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CreationDate NVARCHAR(19)     NOT NULL,
        ModifiedDate NVARCHAR(19)     NOT NULL,
        SessionId    UNIQUEIDENTIFIER NOT NULL,
        StudentId    UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        IsPresent    BIT              NOT NULL DEFAULT 1,
        IsManual     BIT              NOT NULL DEFAULT 0,
        AttendedAt   NVARCHAR(19)     NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        CONSTRAINT PK_AttendanceRecord PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UQ_AttendanceRecord_Session_Student ON AttendanceRecord(SessionId, StudentId);
    CREATE INDEX IX_AttendanceRecord_Session  ON AttendanceRecord(SessionId);
    CREATE INDEX IX_AttendanceRecord_Student  ON AttendanceRecord(StudentId);
    CREATE INDEX IX_AttendanceRecord_School   ON AttendanceRecord(SchoolId);
END
GO

PRINT 'Script v13 (Attendance) applied successfully.';
