-- ========================================================================
-- TechHub — Complete Initial Database Migration
-- ========================================================================
-- This script creates ALL tables, constraints, indexes, and seed data
-- for a fresh TechHub deployment. It is the single source of truth.
-- ========================================================================

-- ========================================================================
-- SECTION 1: CORE TABLES
-- ========================================================================

-- 1.1 School
IF OBJECT_ID('School', 'U') IS NULL
BEGIN
    CREATE TABLE School (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CreationDate    DATETIME2        NOT NULL,
        ModifiedDate    DATETIME2        NOT NULL,
        SchoolName      NVARCHAR(500)    NOT NULL,
        Location        NVARCHAR(100)    NULL,
        CountryId       INT              NOT NULL DEFAULT 0,
        StateId         INT              NOT NULL DEFAULT 0,
        Address         NVARCHAR(1000)   NULL,
        HasBranch       BIT              NOT NULL DEFAULT 0,
        ISActive        BIT              NULL,
        LogoUrl         NVARCHAR(MAX)    NULL,
        LogoPublicId    NVARCHAR(500)    NULL,
        CONSTRAINT PK_School PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UQ_School_information
        ON School(SchoolName, Address)
        WHERE Address IS NOT NULL;
END;

-- 1.2 SchoolCode
IF OBJECT_ID('SchoolCode', 'U') IS NULL
BEGIN
    CREATE TABLE SchoolCode (
        SchoolId UNIQUEIDENTIFIER NOT NULL,
        Code     NVARCHAR(80)     NOT NULL,
        CONSTRAINT PK_SchoolCode PRIMARY KEY (SchoolId),
        CONSTRAINT FK_SchoolCode_School FOREIGN KEY (SchoolId) REFERENCES School(Id)
    );
END;

-- 1.3 TenantInfo
IF OBJECT_ID('TenantInfo', 'U') IS NULL
BEGIN
    CREATE TABLE TenantInfo (
        Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        SchoolId         UNIQUEIDENTIFIER NOT NULL,
        Identifier       NVARCHAR(100)    NOT NULL,
        IsActive         BIT              NOT NULL DEFAULT 1,
        ConnectionString NVARCHAR(500)    NULL,
        CreatedDate      DATETIME2        NOT NULL DEFAULT GETDATE(),
        ModifiedDate     DATETIME2        NOT NULL DEFAULT GETDATE(),
        SettingsJson     NVARCHAR(MAX)    NULL,
        CONSTRAINT PK_TenantInfo PRIMARY KEY (Id),
        CONSTRAINT FK_TenantInfo_School FOREIGN KEY (SchoolId) REFERENCES School(Id),
        CONSTRAINT UQ_TenantInfo_Identifier UNIQUE (Identifier),
        CONSTRAINT UQ_TenantInfo_SchoolId UNIQUE (SchoolId)
    );
END;

-- 1.4 Users
IF OBJECT_ID('Users', 'U') IS NULL
BEGIN
    CREATE TABLE Users (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CreationDate  NVARCHAR(19)     NOT NULL,
        ModifiedDate  NVARCHAR(19)     NOT NULL,
        FirstName     NVARCHAR(100)    NULL,
        MiddleName    NVARCHAR(100)    NULL,
        LastName      NVARCHAR(100)    NULL,
        EmailAddress  NVARCHAR(255)    NULL,
        HashPassword  NVARCHAR(255)    NULL,
        IsActive      BIT              NOT NULL DEFAULT 1,
        HasAccess     BIT              NOT NULL DEFAULT 1,
        UserName      NVARCHAR(100)    NULL,
        SchoolCode    NVARCHAR(50)     NULL,
        SchoolId      UNIQUEIDENTIFIER NOT NULL,
        RoleId        INT              NOT NULL,
        CreatedBy     UNIQUEIDENTIFIER NOT NULL,
        ProfileImage  NVARCHAR(MAX)    NULL,
        GuardianName  NVARCHAR(MAX)    NULL,
        DOB           NVARCHAR(19)     NULL,
        LineManagerId UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id, CreationDate)
    );
END;

-- 1.5 PlatformUser
IF OBJECT_ID('PlatformUser', 'U') IS NULL
BEGIN
    CREATE TABLE PlatformUser (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        FirstName    NVARCHAR(100)    NOT NULL,
        LastName     NVARCHAR(100)    NOT NULL,
        Email        NVARCHAR(200)    NOT NULL,
        Username     NVARCHAR(100)    NOT NULL,
        PasswordHash NVARCHAR(500)    NOT NULL,
        Role         NVARCHAR(20)     NOT NULL DEFAULT 'PlatformAdmin',
        IsActive     BIT              NOT NULL DEFAULT 1,
        IsDeleted    BIT              NOT NULL DEFAULT 0,
        CreatedBy    UNIQUEIDENTIFIER NULL,
        CreatedAt    NVARCHAR(30)     NOT NULL,
        ModifiedAt   NVARCHAR(30)     NOT NULL
    );

    CREATE UNIQUE INDEX IX_PlatformUsers_Username
        ON PlatformUser(Username) WHERE IsDeleted = 0;
    CREATE UNIQUE INDEX IX_PlatformUsers_Email
        ON PlatformUser(Email) WHERE IsDeleted = 0;
END;

-- 1.6 LoginHistory (table only — no seed data)
IF OBJECT_ID('LoginHistory', 'U') IS NULL
BEGIN
    CREATE TABLE LoginHistory (
        Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CreationDate  NVARCHAR(19)     NOT NULL,
        ModifiedDate  NVARCHAR(19)     NOT NULL,
        UserId        UNIQUEIDENTIFIER NULL,
        RoleId        INT              NOT NULL,
        PasswordFailed BIT             NOT NULL,
        DeviceType    NVARCHAR(MAX)    NULL,
        DeviceIp      NVARCHAR(MAX)    NULL
    );

    CREATE CLUSTERED INDEX IX_LoginHistory_UserId_CreationDate
        ON LoginHistory(UserId, CreationDate DESC);
END;

-- 1.7 RefreshTokens
IF OBJECT_ID('RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE RefreshTokens (
        Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserId           UNIQUEIDENTIFIER NOT NULL,
        SchoolId         UNIQUEIDENTIFIER NOT NULL,
        Token            NVARCHAR(500)    NOT NULL,
        ExpiresAt        DATETIME2        NOT NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        RevokedAt        DATETIME2        NULL,
        ReplacedByToken  NVARCHAR(500)    NULL,
        IsRevoked        BIT              NOT NULL DEFAULT 0,
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_RefreshTokens_Token  ON RefreshTokens(Token);
    CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
END;

-- 1.8 AdminPermissions
IF OBJECT_ID('AdminPermissions', 'U') IS NULL
BEGIN
    CREATE TABLE AdminPermissions (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserId       UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        Permissions  INT              NOT NULL DEFAULT 0,
        CreationDate DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDate DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              DEFAULT 1,
        CONSTRAINT FK_AdminPermissions_User FOREIGN KEY (UserId) REFERENCES Users(Id),
        CONSTRAINT FK_AdminPermissions_School FOREIGN KEY (SchoolId) REFERENCES School(Id),
        CONSTRAINT FK_AdminPermissions_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
        CONSTRAINT UQ_AdminPermissions_User_School UNIQUE (UserId, SchoolId)
    );

    CREATE INDEX IX_AdminPermissions_UserId      ON AdminPermissions(UserId);
    CREATE INDEX IX_AdminPermissions_SchoolId     ON AdminPermissions(SchoolId);
    CREATE INDEX IX_AdminPermissions_Permissions  ON AdminPermissions(Permissions);
END;

-- 1.9 State
IF OBJECT_ID('State', 'U') IS NULL
BEGIN
    CREATE TABLE State (
        Id        INT           NOT NULL PRIMARY KEY,
        States    NVARCHAR(MAX) NOT NULL,
        CountryId INT           NOT NULL
    );
END;

GO

-- ========================================================================
-- SECTION 2: CLASSROOM & SUBJECT TABLES
-- ========================================================================

-- 2.1 Classroom (mapped as StudentClass in EF)
IF OBJECT_ID('Classroom', 'U') IS NULL
BEGIN
    CREATE TABLE Classroom (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CreationDate  NVARCHAR(19)     NOT NULL,
        ModifiedDate  NVARCHAR(19)     NOT NULL,
        Name          NVARCHAR(255)    NOT NULL,
        TeacherName   NVARCHAR(255)    NULL,
        TeacherId     UNIQUEIDENTIFIER NOT NULL,
        NoOfStudents  INT              NOT NULL DEFAULT 0,
        CreatedBy     UNIQUEIDENTIFIER NOT NULL,
        SchoolId      UNIQUEIDENTIFIER NOT NULL,
        IsActive      BIT              NOT NULL DEFAULT 1,
        CONSTRAINT PK_Classroom PRIMARY KEY (Id, CreationDate)
    );

    CREATE UNIQUE INDEX UQ_Classroom_SchoolId_Name
        ON Classroom(SchoolId, Name);
END;

-- 2.2 Subjects
IF OBJECT_ID('Subjects', 'U') IS NULL
BEGIN
    CREATE TABLE Subjects (
        Id             UNIQUEIDENTIFIER NOT NULL,
        CreationDate   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDate   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Subject        NVARCHAR(255)    NOT NULL,
        Category       INT              NOT NULL,
        ClassCategory  INT              NOT NULL,
        SchoolId       UNIQUEIDENTIFIER NOT NULL,
        CreatedBy      UNIQUEIDENTIFIER NOT NULL,
        IsActive       BIT              NOT NULL DEFAULT 1,
        CONSTRAINT PK_Subjects PRIMARY KEY (Id, SchoolId)
    );

    CREATE INDEX IX_Subjects_SchoolId ON Subjects(SchoolId);
END;

-- 2.3 ClassroomSubject
IF OBJECT_ID('ClassroomSubject', 'U') IS NULL
BEGIN
    CREATE TABLE ClassroomSubject (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        CreationDate DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
        ModifiedDate DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
        ClassroomId  UNIQUEIDENTIFIER NOT NULL,
        SubjectId    UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_ClassroomSubjects_Classroom FOREIGN KEY (ClassroomId) REFERENCES Classroom(Id),
        CONSTRAINT FK_ClassroomSubjects_Subject   FOREIGN KEY (SubjectId)   REFERENCES Subjects(Id),
        CONSTRAINT FK_ClassroomSubjects_School    FOREIGN KEY (SchoolId)    REFERENCES School(Id)
    );
END;

-- 2.4 ClassroomTeacher
IF OBJECT_ID('ClassroomTeacher', 'U') IS NULL
BEGIN
    CREATE TABLE ClassroomTeacher (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        ClassroomId  UNIQUEIDENTIFIER NOT NULL,
        TeacherId    UNIQUEIDENTIFIER NOT NULL,
        IsPrimary    BIT              DEFAULT 0,
        CreationDate DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDate DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              DEFAULT 1,
        CONSTRAINT FK_ClassroomTeacher_Classroom FOREIGN KEY (ClassroomId) REFERENCES Classroom(Id),
        CONSTRAINT FK_ClassroomTeacher_Teacher   FOREIGN KEY (TeacherId)   REFERENCES Users(Id),
        CONSTRAINT FK_ClassroomTeacher_School    FOREIGN KEY (SchoolId)    REFERENCES School(Id),
        CONSTRAINT FK_ClassroomTeacher_CreatedBy FOREIGN KEY (CreatedBy)   REFERENCES Users(Id),
        CONSTRAINT UQ_ClassroomTeacher_Classroom_Teacher UNIQUE (ClassroomId, TeacherId)
    );
END;

-- 2.5 TeacherSubject
IF OBJECT_ID('TeacherSubject', 'U') IS NULL
BEGIN
    CREATE TABLE TeacherSubject (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CreationDate NVARCHAR(MAX)    NOT NULL,
        ModifiedDate NVARCHAR(MAX)    NOT NULL,
        TeacherId    UNIQUEIDENTIFIER NOT NULL,
        SubjectId    UNIQUEIDENTIFIER NOT NULL,
        ClassroomId  UNIQUEIDENTIFIER NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT UC_TeacherSubject_UniqueActive UNIQUE (TeacherId, SubjectId, ClassroomId, IsActive)
    );
END;

-- 2.6 StudentClassroom
IF OBJECT_ID('StudentClassroom', 'U') IS NULL
BEGIN
    CREATE TABLE StudentClassroom (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CreationDate VARCHAR(19)      NOT NULL,
        ModifiedDate VARCHAR(19)      NOT NULL,
        StudentId    UNIQUEIDENTIFIER NOT NULL,
        ClassroomId  UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT UC_StudentClassroom_UniqueActive UNIQUE (StudentId, ClassroomId, IsActive)
    );
END;

-- 2.7 StudentMinorSubject
IF OBJECT_ID('StudentMinorSubject', 'U') IS NULL
BEGIN
    CREATE TABLE StudentMinorSubject (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CreationDate VARCHAR(19)      NOT NULL,
        ModifiedDate VARCHAR(19)      NOT NULL,
        StudentId    UNIQUEIDENTIFIER NOT NULL,
        SubjectId    UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        IsActive     BIT              NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT UC_StudentMinorSubject_UniqueActive UNIQUE (StudentId, SubjectId, IsActive)
    );
END;

-- 2.8 StudentCourses
IF OBJECT_ID('StudentCourses', 'U') IS NULL
BEGIN
    CREATE TABLE StudentCourses (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CreationDate NVARCHAR(19)     NOT NULL,
        ModifiedDate NVARCHAR(19)     NOT NULL,
        ClassroomId  UNIQUEIDENTIFIER NOT NULL,
        StudentId    UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        Status       INT              NOT NULL
    );
END;

GO

-- ========================================================================
-- SECTION 3: TOPIC & SUBTOPIC
-- ========================================================================

-- 3.1 Topic
IF OBJECT_ID('Topic', 'U') IS NULL
BEGIN
    CREATE TABLE Topic (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SubjectId    UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        ClassroomId  UNIQUEIDENTIFIER NOT NULL,
        Name         NVARCHAR(200)    NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        IsDeleted    BIT              NOT NULL DEFAULT 0,
        CreatedAt    NVARCHAR(19)     NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT FK_Topic_Subject FOREIGN KEY (SubjectId) REFERENCES Subjects(Id)
    );

    CREATE INDEX IX_Topic_SubjectId   ON Topic(SubjectId);
    CREATE INDEX IX_Topic_SchoolId     ON Topic(SchoolId);
    CREATE INDEX IX_Topic_ClassroomId  ON Topic(SchoolId, ClassroomId);
END;

-- 3.2 SubTopic
IF OBJECT_ID('SubTopic', 'U') IS NULL
BEGIN
    CREATE TABLE SubTopic (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TopicId      UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        ClassroomId  UNIQUEIDENTIFIER NOT NULL,
        Name         NVARCHAR(200)    NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        IsDeleted    BIT              NOT NULL DEFAULT 0,
        CreatedAt    NVARCHAR(19)     NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT FK_SubTopic_Topic FOREIGN KEY (TopicId) REFERENCES Topic(Id)
    );

    CREATE INDEX IX_SubTopic_TopicId      ON SubTopic(TopicId);
    CREATE INDEX IX_SubTopic_SchoolId     ON SubTopic(SchoolId);
    CREATE INDEX IX_SubTopic_ClassroomId  ON SubTopic(SchoolId, ClassroomId);
END;

GO

-- ========================================================================
-- SECTION 4: LESSON PLANNING
-- ========================================================================

-- 4.1 LessonContent
IF OBJECT_ID('LessonContent', 'U') IS NULL
BEGIN
    CREATE TABLE LessonContent (
        Id                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SchoolId          UNIQUEIDENTIFIER NOT NULL,
        ClassroomId       UNIQUEIDENTIFIER NOT NULL,
        SubjectId         UNIQUEIDENTIFIER NOT NULL,
        TopicId           UNIQUEIDENTIFIER NOT NULL,
        SubTopic          NVARCHAR(250)    NULL,
        SubTopicId        UNIQUEIDENTIFIER NOT NULL,
        Aim               NVARCHAR(500)    NOT NULL,
        Description       NVARCHAR(2000)   NOT NULL,
        Status            NVARCHAR(30)     NOT NULL DEFAULT 'PendingApproval',
        CreatedBy         UNIQUEIDENTIFIER NOT NULL,
        ApprovedBy        UNIQUEIDENTIFIER NULL,
        RejectedBy        UNIQUEIDENTIFIER NULL,
        RejectionReason   NVARCHAR(500)    NULL,
        CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ApprovedAt        DATETIME2        NULL,
        QuizCode          NVARCHAR(20)     NULL,
        AccessDate        DATE             NULL,
        AccessTime        TIME             NULL,
        DurationMinutes   INT              NULL,
        AccessEndsAt      DATETIME2        NULL,
        AssessmentSetId   UNIQUEIDENTIFIER NULL,
        IsActive          BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_LessonContent_Classroom FOREIGN KEY (ClassroomId) REFERENCES Classroom(Id),
        CONSTRAINT FK_LessonContent_CreatedBy FOREIGN KEY (CreatedBy)   REFERENCES Users(Id)
    );

    CREATE INDEX IX_LessonContent_ClassroomId   ON LessonContent(ClassroomId, Status);
    CREATE INDEX IX_LessonContent_CreatedBy     ON LessonContent(CreatedBy);
    CREATE INDEX IX_LessonContent_AssessmentSet ON LessonContent(AssessmentSetId);
    CREATE INDEX IX_LessonContent_SubjectId_SchoolId
        ON LessonContent(SubjectId, SchoolId)
        INCLUDE (ClassroomId, TopicId, SubTopic)
        WHERE IsActive = 1;
END;

-- 4.2 LessonMedia
IF OBJECT_ID('LessonMedia', 'U') IS NULL
BEGIN
    CREATE TABLE LessonMedia (
        Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        LessonContentId  UNIQUEIDENTIFIER NOT NULL,
        SchoolId         UNIQUEIDENTIFIER NOT NULL,
        FileName         NVARCHAR(300)    NOT NULL,
        OriginalFileName NVARCHAR(300)    NOT NULL,
        FileExtension    NVARCHAR(20)     NOT NULL,
        MediaType        NVARCHAR(20)     NOT NULL,
        FileSizeBytes    BIGINT           NOT NULL,
        CloudinaryUrl    NVARCHAR(1000)   NOT NULL,
        PublicId         NVARCHAR(500)    NOT NULL,
        Duration         INT              NULL,
        Status           NVARCHAR(20)     NOT NULL DEFAULT 'Ready',
        DisplayOrder     INT              NOT NULL DEFAULT 0,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsActive         BIT              NOT NULL DEFAULT 1,
        MetaData         NVARCHAR(3000)   NULL,
        CONSTRAINT FK_LessonMedia_LessonContent FOREIGN KEY (LessonContentId)
            REFERENCES LessonContent(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_LessonMedia_LessonContentId ON LessonMedia(LessonContentId);
END;

-- 4.3 ClassPreparation
IF OBJECT_ID('ClassPreparation', 'U') IS NULL
BEGIN
    CREATE TABLE ClassPreparation (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        ClassroomId             UNIQUEIDENTIFIER NOT NULL,
        SubjectId               UNIQUEIDENTIFIER NOT NULL,
        TeacherId               UNIQUEIDENTIFIER NOT NULL,
        SchoolId                UNIQUEIDENTIFIER NOT NULL,
        Title                   NVARCHAR(200)    NOT NULL,
        TopicId                 UNIQUEIDENTIFIER NOT NULL,
        SubTopicId              UNIQUEIDENTIFIER NULL,
        AimAndObjectives        NVARCHAR(MAX)    NOT NULL,
        ScheduledDate           DATE             NULL,
        ScheduledTime           TIME             NULL,
        DurationMinutes         INT              NULL,
        ClassType               INT              NOT NULL,
        Status                  INT              NOT NULL DEFAULT 0,
        SubmittedForApprovalDate DATETIME        NULL,
        SubmittedBy             UNIQUEIDENTIFIER NULL,
        ApprovedBy              UNIQUEIDENTIFIER NULL,
        ApprovedDate            DATETIME         NULL,
        RejectedBy              UNIQUEIDENTIFIER NULL,
        RejectedDate            DATETIME         NULL,
        RejectionReason         NVARCHAR(500)    NULL,
        MediaMetadataJson       NVARCHAR(MAX)    NULL,
        ApprovalNotes           NVARCHAR(1000)   NULL,
        ApprovalTimeMinutes     INT              NULL,
        IsUrgent                BIT              NOT NULL DEFAULT 0,
        NeedsReview             BIT              NOT NULL DEFAULT 0,
        AutoApprovalEligible    BIT              NOT NULL DEFAULT 0,
        CreationDate            DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDate            DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy               UNIQUEIDENTIFIER NOT NULL,
        IsActive                BIT              DEFAULT 1,
        CONSTRAINT FK_ClassPreparation_Classroom FOREIGN KEY (ClassroomId) REFERENCES Classroom(Id),
        CONSTRAINT FK_ClassPreparation_Subject   FOREIGN KEY (SubjectId)   REFERENCES Subjects(Id),
        CONSTRAINT FK_ClassPreparation_Teacher   FOREIGN KEY (TeacherId)   REFERENCES Users(Id),
        CONSTRAINT FK_ClassPreparation_School    FOREIGN KEY (SchoolId)    REFERENCES School(Id)
    );

    CREATE INDEX IX_ClassPreparation_Status         ON ClassPreparation(Status);
    CREATE INDEX IX_ClassPreparation_SchoolId_Status ON ClassPreparation(SchoolId, Status);
    CREATE INDEX IX_ClassPreparation_TeacherId       ON ClassPreparation(TeacherId);
    CREATE INDEX IX_ClassPreparation_ClassroomId     ON ClassPreparation(ClassroomId);
    CREATE INDEX IX_ClassPreparation_ScheduledDate   ON ClassPreparation(ScheduledDate) WHERE ScheduledDate IS NOT NULL;
    CREATE INDEX IX_ClassPreparation_ApprovalQueue
        ON ClassPreparation(Status, IsUrgent DESC, SubmittedForApprovalDate DESC)
        WHERE Status = 1;
    CREATE INDEX IX_ClassPreparation_AutoApprovalEligible
        ON ClassPreparation(AutoApprovalEligible, Status)
        WHERE AutoApprovalEligible = 1 AND Status = 1;
END;

-- 4.4 ClassPreparationMedia
IF OBJECT_ID('ClassPreparationMedia', 'U') IS NULL
BEGIN
    CREATE TABLE ClassPreparationMedia (
        Id                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        ClassPreparationId UNIQUEIDENTIFIER NULL,
        MediaKey           NVARCHAR(100)    NOT NULL,
        PublicId           NVARCHAR(200)    NULL,
        MediaType          INT              NOT NULL,
        OriginalFileName   NVARCHAR(200)    NOT NULL,
        DisplayName        NVARCHAR(200)    NULL,
        FileSizeBytes      BIGINT           NOT NULL,
        OriginalSizeBytes  BIGINT           NULL,
        DurationSeconds    INT              NULL,
        MimeType           NVARCHAR(100)    NULL,
        FileExtension      NVARCHAR(10)     NULL,
        SHA256Hash         NVARCHAR(64)     NULL,
        CdnUrl             NVARCHAR(500)    NOT NULL,
        ThumbnailUrl       NVARCHAR(500)    NULL,
        PreviewUrl         NVARCHAR(500)    NULL,
        CdnProvider        NVARCHAR(50)     DEFAULT 'Cloudinary',
        IsTemporary        BIT              DEFAULT 1,
        DownloadCount      INT              DEFAULT 0,
        LastDownloadDate   DATETIME         NULL,
        LastDownloadedBy   UNIQUEIDENTIFIER NULL,
        IsDeleted          BIT              DEFAULT 0,
        DeletedDate        DATETIME         NULL,
        DeletedBy          UNIQUEIDENTIFIER NULL,
        DeletionReason     NVARCHAR(500)    NULL,
        UploadStatus       INT              NOT NULL DEFAULT 0,
        UploadErrorMessage NVARCHAR(500)    NULL,
        ModifiedDate       DATETIME         NULL,
        AIAnalysisStatus   INT              NOT NULL DEFAULT 0,
        AIAnalysisData     NVARCHAR(MAX)    NULL,
        KeyMoments         NVARCHAR(MAX)    NULL,
        TranscriptText     NVARCHAR(MAX)    NULL,
        UploadedDate       DATETIME         DEFAULT GETUTCDATE(),
        SchoolId           UNIQUEIDENTIFIER NOT NULL,
        CreationDate       DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy          UNIQUEIDENTIFIER NOT NULL,
        IsActive           BIT              DEFAULT 1,
        CONSTRAINT FK_ClassPreparationMedia_ClassPreparation
            FOREIGN KEY (ClassPreparationId) REFERENCES ClassPreparation(Id) ON DELETE SET NULL,
        CONSTRAINT FK_ClassPreparationMedia_School     FOREIGN KEY (SchoolId)  REFERENCES School(Id),
        CONSTRAINT FK_ClassPreparationMedia_CreatedBy  FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
        CONSTRAINT FK_ClassPreparationMedia_DeletedBy  FOREIGN KEY (DeletedBy) REFERENCES Users(Id),
        CONSTRAINT UQ_ClassPreparationMedia_MediaKey   UNIQUE (MediaKey)
    );

    CREATE INDEX IX_ClassPreparationMedia_ClassPreparationId ON ClassPreparationMedia(ClassPreparationId);
    CREATE INDEX IX_ClassPreparationMedia_MediaKey           ON ClassPreparationMedia(MediaKey);
    CREATE INDEX IX_ClassPreparationMedia_SHA256Hash         ON ClassPreparationMedia(SHA256Hash);
    CREATE INDEX IX_ClassPreparationMedia_IsTemporary        ON ClassPreparationMedia(IsTemporary) WHERE IsTemporary = 1;
    CREATE INDEX IX_ClassPreparationMedia_IsDeleted           ON ClassPreparationMedia(IsDeleted) WHERE IsDeleted = 1;
    CREATE INDEX IX_ClassPreparationMedia_SchoolId            ON ClassPreparationMedia(SchoolId);
    CREATE INDEX IX_ClassPreparationMedia_PublicId            ON ClassPreparationMedia(PublicId) WHERE PublicId IS NOT NULL;
    CREATE INDEX IX_ClassPreparationMedia_UploadStatus        ON ClassPreparationMedia(UploadStatus, CreationDate);
    CREATE INDEX IX_ClassPreparationMedia_AIAnalysisStatus    ON ClassPreparationMedia(AIAnalysisStatus, CreationDate);
END;

-- 4.5 ApprovalRequests
IF OBJECT_ID('ApprovalRequests', 'U') IS NULL
BEGIN
    CREATE TABLE ApprovalRequests (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SchoolId        UNIQUEIDENTIFIER NOT NULL,
        RequestedBy     UNIQUEIDENTIFIER NOT NULL,
        ApproverId      UNIQUEIDENTIFIER NOT NULL,
        OperationType   NVARCHAR(50)     NOT NULL,
        EntityType      NVARCHAR(50)     NOT NULL,
        EntityId        UNIQUEIDENTIFIER NULL,
        Payload         NVARCHAR(MAX)    NOT NULL,
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
        RejectionReason NVARCHAR(500)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        RespondedAt     DATETIME2        NULL,
        ExpiresAt       DATETIME2        NOT NULL,
        CONSTRAINT FK_ApprovalRequests_RequestedBy FOREIGN KEY (RequestedBy) REFERENCES Users(Id),
        CONSTRAINT FK_ApprovalRequests_ApproverId  FOREIGN KEY (ApproverId)  REFERENCES Users(Id)
    );

    CREATE INDEX IX_ApprovalRequests_ApproverId   ON ApprovalRequests(ApproverId, Status);
    CREATE INDEX IX_ApprovalRequests_RequestedBy  ON ApprovalRequests(RequestedBy);
    CREATE INDEX IX_ApprovalRequests_EntityId     ON ApprovalRequests(EntityId);
END;

-- 4.6 TeacherTrustScore
IF OBJECT_ID('TeacherTrustScore', 'U') IS NULL
BEGIN
    CREATE TABLE TeacherTrustScore (
        Id                        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TeacherId                 UNIQUEIDENTIFIER NOT NULL,
        SchoolId                  UNIQUEIDENTIFIER NOT NULL,
        TrustScore                DECIMAL(5,2)     NOT NULL DEFAULT 0,
        TotalClassesSubmitted     INT              NOT NULL DEFAULT 0,
        TotalClassesApproved      INT              NOT NULL DEFAULT 0,
        TotalClassesRejected      INT              NOT NULL DEFAULT 0,
        AverageApprovalTimeMinutes INT             NOT NULL DEFAULT 0,
        FastestApprovalMinutes    INT              NULL,
        SlowestApprovalMinutes    INT              NULL,
        ConsecutiveApprovals      INT              NOT NULL DEFAULT 0,
        BestApprovalStreak        INT              NOT NULL DEFAULT 0,
        LastRejectionDate         DATETIME         NULL,
        LastApprovalDate          DATETIME         NULL,
        LastCalculatedDate        DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        CreationDate              DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDate              DATETIME         NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_TeacherTrustScore_Teacher FOREIGN KEY (TeacherId) REFERENCES Users(Id),
        CONSTRAINT FK_TeacherTrustScore_School  FOREIGN KEY (SchoolId)  REFERENCES School(Id),
        CONSTRAINT UQ_TeacherTrustScore_Teacher_School UNIQUE (TeacherId, SchoolId)
    );

    CREATE INDEX IX_TeacherTrustScore_TeacherId   ON TeacherTrustScore(TeacherId);
    CREATE INDEX IX_TeacherTrustScore_TrustScore  ON TeacherTrustScore(TrustScore DESC);
    CREATE INDEX IX_TeacherTrustScore_School      ON TeacherTrustScore(SchoolId);
END;

-- 4.7 StudentLessonProgress
IF OBJECT_ID('StudentLessonProgress', 'U') IS NULL
BEGIN
    CREATE TABLE StudentLessonProgress (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        StudentId    UNIQUEIDENTIFIER NOT NULL,
        LessonId     UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        WatchedAt    NVARCHAR(30)     NOT NULL,
        CreationDate NVARCHAR(30)     NOT NULL
    );

    CREATE UNIQUE INDEX IX_StudentLessonProgress_Student_Lesson
        ON StudentLessonProgress(StudentId, LessonId, SchoolId);
    CREATE INDEX IX_StudentLessonProgress_LessonId
        ON StudentLessonProgress(LessonId);
END;

GO

-- ========================================================================
-- SECTION 5: QUIZ SYSTEM
-- ========================================================================

-- 5.1 Quiz
IF OBJECT_ID('Quiz', 'U') IS NULL
BEGIN
    CREATE TABLE Quiz (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        Code         NVARCHAR(20)     NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        AssessmentSetId UNIQUEIDENTIFIER NULL,
        CreationDate NVARCHAR(30)     NOT NULL,
        ModifiedDate NVARCHAR(30)     NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1
    );

    CREATE UNIQUE INDEX IX_Quiz_Code ON Quiz(Code);
END;

-- 5.2 QuizQuestion
IF OBJECT_ID('QuizQuestion', 'U') IS NULL
BEGIN
    CREATE TABLE QuizQuestion (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        QuizId       UNIQUEIDENTIFIER NOT NULL,
        QuestionId   UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        DisplayOrder INT              NOT NULL DEFAULT 0,
        CreationDate NVARCHAR(30)     NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_QuizQuestion_Quiz FOREIGN KEY (QuizId) REFERENCES Quiz(Id)
    );

    CREATE INDEX IX_QuizQuestion_QuizId ON QuizQuestion(QuizId);
END;

-- 5.3 QuizConfig
IF OBJECT_ID('QuizConfig', 'U') IS NULL
BEGIN
    CREATE TABLE QuizConfig (
        Id                        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TeacherId                 UNIQUEIDENTIFIER NOT NULL,
        SchoolId                  UNIQUEIDENTIFIER NOT NULL,
        AllowRetakes              BIT              NOT NULL DEFAULT 0,
        MaxAttempts               INT              NOT NULL DEFAULT 1,
        PassMarkPercent           INT              NOT NULL DEFAULT 50,
        TimeLimitMinutes          INT              NULL,
        AutoSubmitOnTimeout       BIT              NOT NULL DEFAULT 1,
        ShuffleQuestions          BIT              NOT NULL DEFAULT 0,
        ShowResultImmediately     BIT              NOT NULL DEFAULT 1,
        ShowCorrectAnswers        BIT              NOT NULL DEFAULT 0,
        AllowBoardAnswer          BIT              NOT NULL DEFAULT 1,
        AllowAIAssistance         BIT              NOT NULL DEFAULT 0,
        MaxAIAssistancePerQuestion INT             NOT NULL DEFAULT 1000,
        EasyMarks                 INT              NOT NULL DEFAULT 1,
        MediumMarks               INT              NOT NULL DEFAULT 2,
        HardMarks                 INT              NOT NULL DEFAULT 3,
        ExamLevelMarks            INT              NOT NULL DEFAULT 5,
        DefaultAssessmentSetId    UNIQUEIDENTIFIER NULL,
        CreatedBy                 UNIQUEIDENTIFIER NOT NULL,
        CreationDate              NVARCHAR(30)     NOT NULL,
        ModifiedDate              NVARCHAR(30)     NOT NULL,
        IsActive                  BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_QuizConfig_DefaultAssessmentSet
            FOREIGN KEY (DefaultAssessmentSetId) REFERENCES AssessmentSet(Id)
    );

    CREATE INDEX IX_QuizConfig_Teacher ON QuizConfig(TeacherId, SchoolId, IsActive);
END;

-- 5.4 AssessmentSet
IF OBJECT_ID('AssessmentSet', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentSet (
        Id                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        Name                 NVARCHAR(100)    NOT NULL,
        Label                NVARCHAR(50)     NOT NULL DEFAULT 'Quiz',
        TeacherId            UNIQUEIDENTIFIER NOT NULL,
        SchoolId             UNIQUEIDENTIFIER NOT NULL,
        AllowRetakes         BIT              NOT NULL DEFAULT 0,
        MaxAttempts          INT              NOT NULL DEFAULT 1,
        PassMarkPercent      INT              NOT NULL DEFAULT 50,
        TimeLimitMinutes     INT              NULL,
        AutoSubmitOnTimeout  BIT              NOT NULL DEFAULT 1,
        ShuffleQuestions     BIT              NOT NULL DEFAULT 0,
        ShowResultMode       NVARCHAR(20)     NOT NULL DEFAULT 'Immediate',
        ShowCorrectAnswers   BIT              NOT NULL DEFAULT 0,
        AllowBoardAnswer     BIT              NOT NULL DEFAULT 1,
        EasyMarks            INT              NOT NULL DEFAULT 1,
        MediumMarks          INT              NOT NULL DEFAULT 2,
        HardMarks            INT              NOT NULL DEFAULT 3,
        ExamLevelMarks       INT              NOT NULL DEFAULT 5,
        IsActive             BIT              NOT NULL DEFAULT 1,
        CreationDate         NVARCHAR(30)     NOT NULL,
        ModifiedDate         NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_AssessmentSet_Teacher ON AssessmentSet(TeacherId, SchoolId, IsActive);
END;

-- 5.5 QuizAttempt
IF OBJECT_ID('QuizAttempt', 'U') IS NULL
BEGIN
    CREATE TABLE QuizAttempt (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        QuizCode            NVARCHAR(20)     NOT NULL,
        LessonId            UNIQUEIDENTIFIER NOT NULL,
        StudentId           UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        AttemptNumber       INT              NOT NULL DEFAULT 1,
        StartedAt           NVARCHAR(30)     NOT NULL,
        SubmittedAt         NVARCHAR(30)     NULL,
        TimeTakenSeconds    INT              NULL,
        TotalQuestions      INT              NOT NULL DEFAULT 0,
        TotalAutoGraded     INT              NOT NULL DEFAULT 0,
        TotalManualGraded   INT              NOT NULL DEFAULT 0,
        TotalCorrect        INT              NOT NULL DEFAULT 0,
        TotalWrong          INT              NOT NULL DEFAULT 0,
        TotalSkipped        INT              NOT NULL DEFAULT 0,
        AutoMarksObtained   DECIMAL(10,2)    NOT NULL DEFAULT 0,
        ManualMarksObtained DECIMAL(10,2)    NOT NULL DEFAULT 0,
        TotalMarks          DECIMAL(10,2)    NOT NULL DEFAULT 0,
        FinalScorePercent   DECIMAL(5,2)     NULL,
        IsPassed            BIT              NULL,
        Status              NVARCHAR(20)     NOT NULL DEFAULT 'InProgress',
        DeviceId            NVARCHAR(100)    NULL,
        UserAgent           NVARCHAR(500)    NULL,
        CreationDate        NVARCHAR(30)     NOT NULL,
        ModifiedDate        NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_QuizAttempt_Student ON QuizAttempt(StudentId, LessonId, SchoolId);
    CREATE INDEX IX_QuizAttempt_Status   ON QuizAttempt(Status, SchoolId);
    CREATE INDEX IX_QuizAttempt_Lesson   ON QuizAttempt(LessonId, SchoolId, Status);
    CREATE INDEX IX_QuizAttempt_PerformanceLookup
        ON QuizAttempt(LessonId, SchoolId, Status)
        INCLUDE (FinalScorePercent, IsPassed, StudentId, ModifiedDate);
END;

-- 5.6 QuizAttemptAnswer
IF OBJECT_ID('QuizAttemptAnswer', 'U') IS NULL
BEGIN
    CREATE TABLE QuizAttemptAnswer (
        Id                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AttemptId            UNIQUEIDENTIFIER NOT NULL,
        QuestionId           UNIQUEIDENTIFIER NOT NULL,
        SchoolId             UNIQUEIDENTIFIER NOT NULL,
        QuestionType         INT              NOT NULL,
        SelectedOptionId     UNIQUEIDENTIFIER NULL,
        IsCorrect            BIT              NULL,
        AutoMarksObtained    DECIMAL(10,2)    NULL,
        TypedAnswer          NVARCHAR(MAX)    NULL,
        BoardSessionId       NVARCHAR(100)    NULL,
        AudioUrl             NVARCHAR(500)    NULL,
        ManualMarksObtained  DECIMAL(10,2)    NULL,
        TeacherFeedback      NVARCHAR(1000)   NULL,
        GradedBy             UNIQUEIDENTIFIER NULL,
        GradedAt             NVARCHAR(30)     NULL,
        MaxMarks             DECIMAL(10,2)    NOT NULL DEFAULT 0,
        TimeTakenMs          BIGINT           NULL,
        IsSkipped            BIT              NOT NULL DEFAULT 0,
        CreationDate         NVARCHAR(30)     NOT NULL,
        ModifiedDate         NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_QuizAttemptAnswer_Attempt ON QuizAttemptAnswer(AttemptId, SchoolId);
    CREATE INDEX IX_QuizAttemptAnswer_Grading ON QuizAttemptAnswer(AttemptId, QuestionType, IsSkipped, ManualMarksObtained);
END;

-- 5.7 QuizAttemptAssistance
IF OBJECT_ID('QuizAttemptAssistance', 'U') IS NULL
BEGIN
    CREATE TABLE QuizAttemptAssistance (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AttemptId     UNIQUEIDENTIFIER NOT NULL,
        QuestionId    UNIQUEIDENTIFIER NOT NULL,
        StudentId     UNIQUEIDENTIFIER NOT NULL,
        SchoolId      UNIQUEIDENTIFIER NOT NULL,
        StudentPrompt NVARCHAR(MAX)    NULL,
        AIResponse    NVARCHAR(MAX)    NULL,
        TokensUsed    INT              NOT NULL DEFAULT 0,
        CreatedAt     NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_QuizAttemptAssistance_Attempt ON QuizAttemptAssistance(AttemptId, StudentId);
END;

GO

-- ========================================================================
-- SECTION 6: ASSESSMENT SYSTEM
-- ========================================================================

-- 6.1 Assessment
IF OBJECT_ID('Assessment', 'U') IS NULL
BEGIN
    CREATE TABLE Assessment (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        Code         NVARCHAR(20)     NOT NULL,
        Title        NVARCHAR(200)    NOT NULL,
        Description  NVARCHAR(1000)   NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CreationDate NVARCHAR(30)     NOT NULL,
        ModifiedDate NVARCHAR(30)     NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1
    );

    CREATE UNIQUE INDEX IX_Assessment_Code ON Assessment(Code);
END;

-- 6.2 AssessmentConfig
IF OBJECT_ID('AssessmentConfig', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentConfig (
        Id                    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AssessmentId          UNIQUEIDENTIFIER NOT NULL,
        TimeLimitMinutes      INT              NOT NULL DEFAULT 0,
        ShuffleQuestions      BIT              NOT NULL DEFAULT 0,
        PassMarkPercent       INT              NOT NULL DEFAULT 50,
        ShowResultImmediately BIT              NOT NULL DEFAULT 1,
        ShowCorrectAnswers    BIT              NOT NULL DEFAULT 0,
        ExpiresAt             DATETIME         NULL,
        EasyMarks             INT              NOT NULL DEFAULT 1,
        MediumMarks           INT              NOT NULL DEFAULT 2,
        HardMarks             INT              NOT NULL DEFAULT 3,
        ExamLevelMarks        INT              NOT NULL DEFAULT 5,
        CreatedBy             UNIQUEIDENTIFIER NOT NULL,
        CreationDate          NVARCHAR(30)     NOT NULL,
        ModifiedDate          NVARCHAR(30)     NOT NULL,
        IsActive              BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_AssessmentConfig_Assessment FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );
END;

-- 6.3 AssessmentQuestion
IF OBJECT_ID('AssessmentQuestion', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentQuestion (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AssessmentId UNIQUEIDENTIFIER NOT NULL,
        QuestionId   UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        SubTopicId   UNIQUEIDENTIFIER NULL,
        DisplayOrder INT              NOT NULL DEFAULT 0,
        CreationDate NVARCHAR(30)     NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_AssessmentQuestion_Assessment FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );

    CREATE INDEX IX_AssessmentQuestion_AssessmentId ON AssessmentQuestion(AssessmentId);
END;

-- 6.4 AssessmentAssignment
IF OBJECT_ID('AssessmentAssignment', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentAssignment (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AssessmentId UNIQUEIDENTIFIER NOT NULL,
        TargetType   NVARCHAR(20)     NOT NULL,
        TargetId     UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        CreatedBy    UNIQUEIDENTIFIER NOT NULL,
        CreationDate NVARCHAR(30)     NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_AssessmentAssignment_Assessment FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );

    CREATE INDEX IX_AssessmentAssignment_Target
        ON AssessmentAssignment(TargetType, TargetId, IsActive)
        INCLUDE (AssessmentId);
END;

-- 6.5 AssessmentAttempt
IF OBJECT_ID('AssessmentAttempt', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentAttempt (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AssessmentId        UNIQUEIDENTIFIER NOT NULL,
        StudentId           UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        AttemptNumber       INT              NOT NULL,
        IsOfficial          BIT              NOT NULL DEFAULT 0,
        AutoMarksObtained   DECIMAL(10,2)    NOT NULL DEFAULT 0,
        ManualMarksObtained DECIMAL(10,2)    NOT NULL DEFAULT 0,
        TotalMarks          DECIMAL(10,2)    NOT NULL DEFAULT 0,
        FinalScorePercent   DECIMAL(5,2)     NULL,
        IsPassed            BIT              NULL,
        Status              NVARCHAR(20)     NOT NULL DEFAULT 'InProgress',
        StartedAt           NVARCHAR(30)     NOT NULL,
        SubmittedAt         NVARCHAR(30)     NULL,
        TimeTakenSeconds    INT              NULL,
        CreationDate        NVARCHAR(30)     NOT NULL,
        ModifiedDate        NVARCHAR(30)     NOT NULL,
        CONSTRAINT FK_AssessmentAttempt_Assessment FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );

    CREATE INDEX IX_AssessmentAttempt_Student
        ON AssessmentAttempt(StudentId, AssessmentId, IsOfficial)
        INCLUDE (Status, FinalScorePercent);
END;

-- 6.6 AssessmentAttemptAnswer
IF OBJECT_ID('AssessmentAttemptAnswer', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentAttemptAnswer (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AttemptId           UNIQUEIDENTIFIER NOT NULL,
        QuestionId          UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        QuestionType        INT              NOT NULL,
        SelectedOptionId    UNIQUEIDENTIFIER NULL,
        IsCorrect           BIT              NULL,
        AutoMarksObtained   DECIMAL(10,2)    NULL,
        TypedAnswer         NVARCHAR(MAX)    NULL,
        BoardSessionId      NVARCHAR(100)    NULL,
        AudioUrl            NVARCHAR(500)    NULL,
        ManualMarksObtained DECIMAL(10,2)    NULL,
        TeacherFeedback     NVARCHAR(500)    NULL,
        GradedBy            UNIQUEIDENTIFIER NULL,
        GradedAt            NVARCHAR(30)     NULL,
        MaxMarks            DECIMAL(10,2)    NOT NULL DEFAULT 0,
        TimeTakenMs         BIGINT           NULL,
        IsSkipped           BIT              NOT NULL DEFAULT 0,
        CreationDate        NVARCHAR(30)     NOT NULL,
        ModifiedDate        NVARCHAR(30)     NOT NULL,
        CONSTRAINT FK_AssessmentAttemptAnswer_Attempt
            FOREIGN KEY (AttemptId) REFERENCES AssessmentAttempt(Id)
    );

    CREATE INDEX IX_AssessmentAttemptAnswer_AttemptId ON AssessmentAttemptAnswer(AttemptId);
END;

-- 6.7 AssessmentAttemptAnswerBoard
IF OBJECT_ID('AssessmentAttemptAnswerBoard', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentAttemptAnswerBoard (
        Id             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AnswerId       UNIQUEIDENTIFIER NOT NULL,
        BoardSessionId NVARCHAR(100)    NOT NULL,
        BoardIndex     INT              NULL,
        BoardLabel     NVARCHAR(50)     NULL,
        CreatedAt      NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_AnswerBoard_AnswerId ON AssessmentAttemptAnswerBoard(AnswerId);
    CREATE INDEX IX_AnswerBoard_Session  ON AssessmentAttemptAnswerBoard(BoardSessionId);
END;

GO

-- ========================================================================
-- SECTION 7: QUESTION BANK
-- ========================================================================

-- 7.1 Questions
IF OBJECT_ID('Questions', 'U') IS NULL
BEGIN
    CREATE TABLE Questions (
        Id                    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SchoolId              UNIQUEIDENTIFIER NOT NULL,
        SubjectId             UNIQUEIDENTIFIER NULL,
        TopicId               UNIQUEIDENTIFIER NULL,
        ClassroomId           UNIQUEIDENTIFIER NULL,
        SubTopicId            UNIQUEIDENTIFIER NULL,
        CreatedBy             UNIQUEIDENTIFIER NOT NULL,
        Title                 NVARCHAR(500)    NOT NULL,
        Topic                 NVARCHAR(300)    NULL,
        SubTopic              NVARCHAR(300)    NULL,
        SubjectName           NVARCHAR(200)    NULL,
        TopicName             NVARCHAR(200)    NULL,
        SubTopicName          NVARCHAR(200)    NULL,
        ClassName             NVARCHAR(200)    NULL,
        QuestionType          INT              NOT NULL,
        TextContent           NVARCHAR(MAX)    NULL,
        DifficultyLevel       INT              NOT NULL,
        MarksAllocation       INT              NOT NULL DEFAULT 1,
        QuestionHtml          NVARCHAR(MAX)    NULL,
        ContentParts          NVARCHAR(MAX)    NULL,
        HasLatex              BIT              NOT NULL DEFAULT 0,
        CorrectAnswer         NVARCHAR(MAX)    NULL,
        ImageUrl              NVARCHAR(1000)   NULL,
        ImagePublicId         NVARCHAR(500)    NULL,
        SnapshotUrl           NVARCHAR(500)    NULL,
        SnapshotPublicId      NVARCHAR(200)    NULL,
        JobId                 UNIQUEIDENTIFIER NULL,
        BoardSessionId        UNIQUEIDENTIFIER NULL,
        HasBoardSession       BIT              DEFAULT 0,
        HasMedia              BIT              DEFAULT 0,
        HasAudio              BIT              DEFAULT 0,
        ScanSessionId         UNIQUEIDENTIFIER NULL,
        IsScanned             BIT              DEFAULT 0,
        ExtractedQuestionIndex INT             NULL,
        AIConfidenceScore     NVARCHAR(10)     NULL,
        Status                INT              NOT NULL DEFAULT 0,
        QuestionNumber        INT              NOT NULL DEFAULT 0,
        IsPartial             BIT              NOT NULL DEFAULT 0,
        ReviewedDate          NVARCHAR(50)     NULL,
        ReviewedBy            UNIQUEIDENTIFIER NULL,
        PublishedDate         NVARCHAR(50)     NULL,
        PublishedBy           UNIQUEIDENTIFIER NULL,
        ClientId              NVARCHAR(100)    NULL,
        OriginDevice          NVARCHAR(200)    NULL,
        LastSyncedAt          DATETIME         NULL,
        IsActive              BIT              DEFAULT 1,
        IsDeleted             BIT              DEFAULT 0,
        DeletedDate           NVARCHAR(50)     NULL,
        DeletedBy             UNIQUEIDENTIFIER NULL,
        CreationDate          NVARCHAR(50)     NOT NULL,
        ModifiedDate          NVARCHAR(50)     NULL
    );

    CREATE INDEX IX_Questions_SchoolId     ON Questions(SchoolId);
    CREATE INDEX IX_Questions_SubjectId    ON Questions(SubjectId);
    CREATE INDEX IX_Questions_CreatedBy    ON Questions(CreatedBy);
    CREATE INDEX IX_Questions_Status       ON Questions(Status);
    CREATE INDEX IX_Questions_ClientId     ON Questions(ClientId);
    CREATE INDEX IX_Questions_ScanSessionId ON Questions(ScanSessionId);
    CREATE INDEX IX_Questions_ClassroomId  ON Questions(SchoolId, ClassroomId);
    CREATE INDEX IX_Questions_SubTopicId   ON Questions(SchoolId, SubTopicId);
    CREATE INDEX IX_Questions_TopicId      ON Questions(SchoolId, TopicId);
    CREATE INDEX IX_Questions_JobId        ON Questions(JobId);
END;

-- 7.2 QuestionOptions
IF OBJECT_ID('QuestionOptions', 'U') IS NULL
BEGIN
    CREATE TABLE QuestionOptions (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        QuestionId      UNIQUEIDENTIFIER NOT NULL,
        OptionLabel     NVARCHAR(10)     NOT NULL,
        OptionText      NVARCHAR(MAX)    NOT NULL,
        OptionHtml      NVARCHAR(MAX)    NULL,
        ContentParts    NVARCHAR(MAX)    NULL,
        HasLatex        BIT              NOT NULL DEFAULT 0,
        HasImages       BIT              NOT NULL DEFAULT 0,
        IsCorrect       BIT              DEFAULT 0,
        OrderIndex      INT              NOT NULL DEFAULT 0,
        IsActive        BIT              DEFAULT 1,
        IsDeleted       BIT              DEFAULT 0,
        DeletedDate     NVARCHAR(50)     NULL,
        CreationDate    NVARCHAR(50)     NOT NULL,
        ModifiedDate    NVARCHAR(50)     NULL,
        FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );
END;

-- 7.3 QuestionImages
IF OBJECT_ID('QuestionImages', 'U') IS NULL
BEGIN
    CREATE TABLE QuestionImages (
        Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        QuestionId    UNIQUEIDENTIFIER NOT NULL,
        JobId         UNIQUEIDENTIFIER NOT NULL,
        SchoolId      UNIQUEIDENTIFIER NOT NULL,
        Label         NVARCHAR(100)    NOT NULL,
        CloudinaryUrl NVARCHAR(500)    NOT NULL,
        PublicId      NVARCHAR(200)    NOT NULL,
        DisplayOrder  INT              NOT NULL DEFAULT 0,
        CreatedAt     NVARCHAR(30)     NOT NULL,
        CONSTRAINT FK_QuestionImages_Question FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
    );

    CREATE INDEX IX_QuestionImages_QuestionId ON QuestionImages(QuestionId);
    CREATE INDEX IX_QuestionImages_JobId      ON QuestionImages(JobId);
END;

-- 7.4 QuestionJob
IF OBJECT_ID('QuestionJob', 'U') IS NULL
BEGIN
    CREATE TABLE QuestionJob (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SchoolId        UNIQUEIDENTIFIER NOT NULL,
        SubTopicId      UNIQUEIDENTIFIER NOT NULL,
        TeacherId       UNIQUEIDENTIFIER NOT NULL,
        ClassroomId     UNIQUEIDENTIFIER NULL,
        SubjectId       UNIQUEIDENTIFIER NULL,
        QuestionId      UNIQUEIDENTIFIER NULL,
        QuestionType    NVARCHAR(20)     NOT NULL,
        HasImages       BIT              NOT NULL DEFAULT 0,
        TempImagePath   NVARCHAR(500)    NULL,
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
        FailureReason   NVARCHAR(500)    NULL,
        AttemptCount    INT              NOT NULL DEFAULT 0,
        ExtractedCount  INT              NOT NULL DEFAULT 0,
        FileType        NVARCHAR(20)     NOT NULL DEFAULT 'Image',
        CreatedAt       NVARCHAR(19)     NOT NULL,
        CompletedAt     NVARCHAR(19)     NULL,
        ProcessedAt     NVARCHAR(30)     NULL,
        ProcessedBy     UNIQUEIDENTIFIER NULL,
        CONSTRAINT FK_QuestionJob_SubTopic FOREIGN KEY (SubTopicId) REFERENCES SubTopic(Id)
    );

    CREATE INDEX IX_QuestionJob_Status             ON QuestionJob(Status);
    CREATE INDEX IX_QuestionJob_TeacherId_Status   ON QuestionJob(TeacherId, Status);
    CREATE INDEX IX_QuestionJob_SchoolId           ON QuestionJob(SchoolId);
END;

-- 7.5 ScanSessions
IF OBJECT_ID('ScanSessions', 'U') IS NULL
BEGIN
    CREATE TABLE ScanSessions (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        SchoolId                UNIQUEIDENTIFIER NOT NULL,
        TeacherId               UNIQUEIDENTIFIER NOT NULL,
        OriginalFileUrl         NVARCHAR(MAX)    NULL,
        OriginalFileName        NVARCHAR(500)    NULL,
        FileType                NVARCHAR(20)     NULL,
        FileSizeBytes           BIGINT           DEFAULT 0,
        TotalExtracted          INT              DEFAULT 0,
        TotalConfirmed          INT              DEFAULT 0,
        TotalRejected           INT              DEFAULT 0,
        TotalPending            INT              DEFAULT 0,
        AIModel                 NVARCHAR(100)    NULL,
        ExtractionCompletedDate NVARCHAR(50)     NULL,
        Status                  INT              NOT NULL DEFAULT 0,
        FailureReason           NVARCHAR(MAX)    NULL,
        OriginalFileDeleted     BIT              DEFAULT 0,
        OriginalFileDeletedDate NVARCHAR(50)     NULL,
        IsActive                BIT              DEFAULT 1,
        IsDeleted               BIT              DEFAULT 0,
        DeletedDate             NVARCHAR(50)     NULL,
        CreationDate            NVARCHAR(50)     NOT NULL,
        ModifiedDate            NVARCHAR(50)     NULL,
        CompletedDate           NVARCHAR(50)     NULL
    );

    CREATE INDEX IX_ScanSessions_SchoolId  ON ScanSessions(SchoolId);
    CREATE INDEX IX_ScanSessions_TeacherId ON ScanSessions(TeacherId);
    CREATE INDEX IX_ScanSessions_Status    ON ScanSessions(Status);
END;

-- 7.6 ScanToken
IF OBJECT_ID('ScanToken', 'U') IS NULL
BEGIN
    CREATE TABLE ScanToken (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TeacherId       UNIQUEIDENTIFIER NOT NULL,
        SchoolId        UNIQUEIDENTIFIER NOT NULL,
        Purpose         NVARCHAR(MAX)    DEFAULT 'question_scan',
        ScanType        INT              NOT NULL,
        FileType        NVARCHAR(MAX)    NOT NULL,
        Status          INT              NOT NULL,
        IssuedAt        NVARCHAR(MAX)    NOT NULL,
        ExpiresAt       NVARCHAR(MAX)    NOT NULL,
        UsedAt          NVARCHAR(MAX)    NOT NULL,
        RevokedAt       NVARCHAR(MAX)    NOT NULL,
        RevokeReason    NVARCHAR(MAX)    NOT NULL,
        LocalSessionId  NVARCHAR(MAX)    NOT NULL,
        DeviceId        NVARCHAR(MAX)    NOT NULL,
        TokensConsumed  INT              NULL,
        CreationDate    NVARCHAR(MAX)    NOT NULL
    );
END;

GO

-- ========================================================================
-- SECTION 8: ANALYTICS & PERFORMANCE
-- ========================================================================

-- 8.1 QuizAnalyticsSummary
IF OBJECT_ID('QuizAnalyticsSummary', 'U') IS NULL
BEGIN
    CREATE TABLE QuizAnalyticsSummary (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        LessonId            UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        QuizCode            NVARCHAR(20)     NOT NULL,
        TotalStudents       INT              NOT NULL DEFAULT 0,
        AttemptedCount      INT              NOT NULL DEFAULT 0,
        CompletedCount      INT              NOT NULL DEFAULT 0,
        ParticipationRate   DECIMAL(5,2)     NOT NULL DEFAULT 0,
        AverageScore        DECIMAL(5,2)     NULL,
        HighestScore        DECIMAL(5,2)     NULL,
        LowestScore         DECIMAL(5,2)     NULL,
        PassRate            DECIMAL(5,2)     NULL,
        AverageTimeSeconds  INT              NULL,
        AutoGradedCount     INT              NOT NULL DEFAULT 0,
        PartiallyGradedCount INT             NOT NULL DEFAULT 0,
        FullyGradedCount    INT              NOT NULL DEFAULT 0,
        PendingGradingCount INT              NOT NULL DEFAULT 0,
        ComputedAt          NVARCHAR(30)     NOT NULL,
        PeriodStart         NVARCHAR(30)     NOT NULL,
        PeriodEnd           NVARCHAR(30)     NOT NULL,
        CONSTRAINT UQ_QuizAnalyticsSummary_Lesson_Period UNIQUE (LessonId, PeriodStart, PeriodEnd)
    );

    CREATE INDEX IX_QuizAnalyticsSummary_School ON QuizAnalyticsSummary(SchoolId, ComputedAt DESC);
    CREATE INDEX IX_QuizAnalyticsSummary_Lesson ON QuizAnalyticsSummary(LessonId);
END;

-- 8.2 QuizQuestionAnalytics
IF OBJECT_ID('QuizQuestionAnalytics', 'U') IS NULL
BEGIN
    CREATE TABLE QuizQuestionAnalytics (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        LessonId            UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        QuestionId          UNIQUEIDENTIFIER NOT NULL,
        TotalResponses      INT              NOT NULL DEFAULT 0,
        CorrectCount        INT              NOT NULL DEFAULT 0,
        WrongCount          INT              NOT NULL DEFAULT 0,
        SkippedCount        INT              NOT NULL DEFAULT 0,
        AverageMarksObtained DECIMAL(10,2)   NOT NULL DEFAULT 0,
        SuccessRate         DECIMAL(5,2)     NOT NULL DEFAULT 0,
        AvgTimeTakenMs      BIGINT           NULL,
        ComputedAt          NVARCHAR(30)     NOT NULL,
        CONSTRAINT UQ_QuizQuestionAnalytics_Lesson_Question UNIQUE (LessonId, QuestionId)
    );

    CREATE INDEX IX_QuizQuestionAnalytics_Lesson ON QuizQuestionAnalytics(LessonId);
END;

-- 8.3 QuizStudentAnalytics
IF OBJECT_ID('QuizStudentAnalytics', 'U') IS NULL
BEGIN
    CREATE TABLE QuizStudentAnalytics (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        LessonId            UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        StudentId           UNIQUEIDENTIFIER NOT NULL,
        StudentName         NVARCHAR(200)    NULL,
        AttemptCount        INT              NOT NULL DEFAULT 0,
        BestScorePercent    DECIMAL(5,2)     NULL,
        BestAttemptId       UNIQUEIDENTIFIER NULL,
        IsPassed            BIT              NULL,
        LatestStatus        NVARCHAR(20)     NULL,
        LatestSubmittedAt   NVARCHAR(30)     NULL,
        TotalTimeSeconds    INT              NULL,
        CorrectAnswers      INT              NULL,
        WrongAnswers        INT              NULL,
        SkippedAnswers      INT              NULL,
        ComputedAt          NVARCHAR(30)     NOT NULL,
        CONSTRAINT UQ_QuizStudentAnalytics_Lesson_Student UNIQUE (LessonId, StudentId)
    );

    CREATE INDEX IX_QuizStudentAnalytics_Lesson  ON QuizStudentAnalytics(LessonId);
    CREATE INDEX IX_QuizStudentAnalytics_Student ON QuizStudentAnalytics(StudentId);
END;

-- 8.4 PerformanceAggregationLog
IF OBJECT_ID('PerformanceAggregationLog', 'U') IS NULL
BEGIN
    CREATE TABLE PerformanceAggregationLog (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        SchoolId        UNIQUEIDENTIFIER NOT NULL,
        RunStartedAt    NVARCHAR(20)     NOT NULL,
        RunCompletedAt  NVARCHAR(20)     NULL,
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'Running',
        ErrorMessage    NVARCHAR(MAX)    NULL,
        AttemptNumber   INT              NOT NULL DEFAULT 1,
        ItemsUpserted   INT              NULL,
        CreationDate    NVARCHAR(20)     NOT NULL DEFAULT CONVERT(NVARCHAR(20), GETUTCDATE(), 120)
    );

    CREATE INDEX IX_PerformanceAggregationLog_SchoolId_Status
        ON PerformanceAggregationLog(SchoolId, Status);
END;

GO

-- ========================================================================
-- SECTION 9: MISC TABLES
-- ========================================================================

-- 9.1 EmailTemplates
IF OBJECT_ID('EmailTemplates', 'U') IS NULL
BEGIN
    CREATE TABLE EmailTemplates (
        Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TemplateKey  NVARCHAR(100)    NOT NULL,
        Subject      NVARCHAR(200)    NOT NULL,
        HtmlBody     NVARCHAR(MAX)    NOT NULL,
        IsActive     BIT              DEFAULT 1,
        CreationDate DATETIME         DEFAULT GETUTCDATE(),
        ModifiedDate DATETIME         DEFAULT GETUTCDATE()
    );
END;

GO

-- ========================================================================
-- SECTION 10: SEED DATA
-- ========================================================================

-- 10.1 Seed PlatformSuperAdmin (if not already seeded)
IF NOT EXISTS (SELECT 1 FROM PlatformUser WHERE Username = 'platformadmin')
BEGIN
    INSERT INTO PlatformUser (Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive, IsDeleted, CreatedBy, CreatedAt, ModifiedAt)
    VALUES (
        NEWID(),
        'Platform',
        'Admin',
        'platform@techhub.com',
        'platformadmin',
        '9A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A',
        'PlatformSuperAdmin',
        1,
        0,
        NULL,
        FORMAT(GETUTCDATE(), 'yyyy-MM-dd HH:mm:ss'),
        FORMAT(GETUTCDATE(), 'yyyy-MM-dd HH:mm:ss')
    );
END;

-- ========================================================================
-- SECTION 12: SCHOOL REGISTRATION REQUESTS
-- ========================================================================

IF OBJECT_ID('SchoolRegistrationRequest', 'U') IS NULL
BEGIN
    CREATE TABLE SchoolRegistrationRequest (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        SchoolName      NVARCHAR(500)    NOT NULL,
        Location        NVARCHAR(100)    NULL,
        CountryId       INT              NOT NULL DEFAULT 0,
        StateId         INT              NOT NULL DEFAULT 0,
        [State]         NVARCHAR(100)    NULL,
        Address         NVARCHAR(1000)   NULL,
        HasBranch       BIT              NOT NULL DEFAULT 0,
        TenantIdentifier NVARCHAR(100)   NOT NULL,
        SchoolCode      NVARCHAR(80)     NOT NULL,
        LogoUrl         NVARCHAR(MAX)    NULL,
        LogoPublicId    NVARCHAR(500)    NULL,
        AdminFirstName  NVARCHAR(100)    NOT NULL,
        AdminMiddleName NVARCHAR(100)    NULL,
        AdminLastName   NVARCHAR(100)    NOT NULL,
        AdminEmail      NVARCHAR(255)    NOT NULL,
        AdminUsername   NVARCHAR(100)    NOT NULL,
        AdminPassword   NVARCHAR(255)    NOT NULL,
        [Status]        NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
        RejectionReason NVARCHAR(1000)   NULL,
        ApprovedBy      UNIQUEIDENTIFIER NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        RespondedAt     DATETIME2        NULL,
        CONSTRAINT PK_SchoolRegistrationRequest PRIMARY KEY (Id)
    );

    CREATE INDEX IX_SchoolRegistrationRequest_Status
        ON SchoolRegistrationRequest([Status]);
END;

-- ========================================================================
-- MIGRATION COMPLETE
-- ========================================================================
PRINT 'TechHub initial migration completed successfully.';
