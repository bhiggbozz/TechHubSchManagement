-- Questions Table
CREATE TABLE Questions (
    Id                      UNIQUEIDENTIFIER    PRIMARY KEY DEFAULT NEWID(),
    SchoolId                UNIQUEIDENTIFIER    NOT NULL,
    SubjectId               UNIQUEIDENTIFIER    NOT NULL,
    TopicId                 UNIQUEIDENTIFIER    NULL,
    CreatedBy               UNIQUEIDENTIFIER    NOT NULL,

    -- Content
    Title                   NVARCHAR(500)       NOT NULL,
    Topic                   NVARCHAR(300)       NULL,
    SubTopic                NVARCHAR(300)       NULL,
    QuestionType            INT                 NOT NULL,
    TextContent             NVARCHAR(MAX)       NULL,
    DifficultyLevel         INT                 NOT NULL,
    MarksAllocation         INT                 NOT NULL DEFAULT 1,

    -- Board & Media
    BoardSessionId          UNIQUEIDENTIFIER    NULL,
    HasBoardSession         BIT                 DEFAULT 0,
    HasMedia                BIT                 DEFAULT 0,
    HasAudio                BIT                 DEFAULT 0,

    -- Scan Session
    ScanSessionId           UNIQUEIDENTIFIER    NULL,
    IsScanned               BIT                 DEFAULT 0,
    ExtractedQuestionIndex  INT                 NULL,
    AIConfidenceScore       NVARCHAR(10)        NULL,

    -- Status
    -- Default -1 handled at service layer
    -- based on creation path
    Status                  INT                 NOT NULL DEFAULT 0,

    -- Review Tracking
    ReviewedDate            NVARCHAR(50)        NULL,
    ReviewedBy              UNIQUEIDENTIFIER    NULL,

    -- Publish Tracking
    PublishedDate           NVARCHAR(50)        NULL,
    PublishedBy             UNIQUEIDENTIFIER    NULL,

    -- Sync
    ClientId                NVARCHAR(100)       NULL,
    OriginDevice            NVARCHAR(200)       NULL,
    LastSyncedAt            DATETIME            NULL,

    -- Soft Delete
    IsActive                BIT                 DEFAULT 1,
    IsDeleted               BIT                 DEFAULT 0,
    DeletedDate             NVARCHAR(50)        NULL,
    DeletedBy               UNIQUEIDENTIFIER    NULL,

    -- Audit
    CreationDate            NVARCHAR(50)        NOT NULL,
    ModifiedDate            NVARCHAR(50)        NULL
);

-- Question Options (for MCQ)
CREATE TABLE QuestionOptions (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    QuestionId      UNIQUEIDENTIFIER NOT NULL,
    OptionLabel     NVARCHAR(10) NOT NULL,
    OptionText      NVARCHAR(MAX) NOT NULL,
    IsCorrect       BIT DEFAULT 0,
    OrderIndex      INT NOT NULL DEFAULT 0,

    -- Soft delete
    IsActive        BIT DEFAULT 1,
    IsDeleted       BIT DEFAULT 0,
    DeletedDate     NVARCHAR(50) NULL,

    -- Audit
    CreationDate    NVARCHAR(50) NOT NULL,
    ModifiedDate    NVARCHAR(50) NULL,

    FOREIGN KEY (QuestionId) REFERENCES Questions(Id)
);

-- Indexes
CREATE INDEX IX_Questions_SchoolId 
    ON Questions(SchoolId);
CREATE INDEX IX_Questions_SubjectId 
    ON Questions(SubjectId);
CREATE INDEX IX_Questions_CreatedBy 
    ON Questions(CreatedBy);
CREATE INDEX IX_Questions_Status 
    ON Questions(Status);
CREATE INDEX IX_Questions_ClientId 
    ON Questions(ClientId);
    -- Critical for sync conflict resolution
    ---------------------------------------------------------------------------------------------------------------
    ALTER TABLE Questions
    ADD PublishedDate   NVARCHAR(50)        NULL,
        PublishedBy     UNIQUEIDENTIFIER    NULL,
        DeletedBy       UNIQUEIDENTIFIER    NULL;

------------------------------------------------------------------------
CREATE TABLE ScanSessions (
    Id                      UNIQUEIDENTIFIER    PRIMARY KEY DEFAULT NEWID(),
    SchoolId                UNIQUEIDENTIFIER    NOT NULL,
    TeacherId               UNIQUEIDENTIFIER    NOT NULL,

    -- File Info
    OriginalFileUrl         NVARCHAR(MAX)       NULL,
    OriginalFileName        NVARCHAR(500)       NULL,
    FileType                NVARCHAR(20)        NULL,
    FileSizeBytes           BIGINT              DEFAULT 0,

    -- Extraction Result
    TotalExtracted          INT                 DEFAULT 0,
    TotalConfirmed          INT                 DEFAULT 0,
    TotalRejected           INT                 DEFAULT 0,
    TotalPending            INT                 DEFAULT 0,
    AIModel                 NVARCHAR(100)       NULL,
    ExtractionCompletedDate NVARCHAR(50)        NULL,

    -- Status
    Status                  INT                 NOT NULL DEFAULT 0,
    FailureReason           NVARCHAR(MAX)       NULL,

    -- CDN Cleanup
    OriginalFileDeleted     BIT                 DEFAULT 0,
    OriginalFileDeletedDate NVARCHAR(50)        NULL,

    -- Soft Delete
    IsActive                BIT                 DEFAULT 1,
    IsDeleted               BIT                 DEFAULT 0,
    DeletedDate             NVARCHAR(50)        NULL,

    -- Audit
    CreationDate            NVARCHAR(50)        NOT NULL,
    ModifiedDate            NVARCHAR(50)        NULL,
    CompletedDate           NVARCHAR(50)        NULL
);

-- Indexes
CREATE INDEX IX_Questions_SchoolId
    ON Questions(SchoolId);
CREATE INDEX IX_Questions_SubjectId
    ON Questions(SubjectId);
CREATE INDEX IX_Questions_CreatedBy
    ON Questions(CreatedBy);
CREATE INDEX IX_Questions_Status
    ON Questions(Status);
CREATE INDEX IX_Questions_ClientId
    ON Questions(ClientId);
CREATE INDEX IX_Questions_ScanSessionId
    ON Questions(ScanSessionId);
-- Critical for fetching all questions
-- from one scan session during review

CREATE INDEX IX_ScanSessions_SchoolId
    ON ScanSessions(SchoolId);
CREATE INDEX IX_ScanSessions_TeacherId
    ON ScanSessions(TeacherId);
CREATE INDEX IX_ScanSessions_Status
    ON ScanSessions(Status);

--------------------------------------------------------

ALTER TABLE Questions
    ADD SnapshotUrl      NVARCHAR(MAX) NULL,
        SnapshotPublicId NVARCHAR(500) NULL;
---------------------------------------------------------

ALTER TABLE QuestionJob
ADD ExtractedCount INT NOT NULL DEFAULT 0;

CREATE TABLE QuestionJob (
    Id            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    SchoolId      UNIQUEIDENTIFIER NOT NULL,
    SubTopicId    UNIQUEIDENTIFIER NOT NULL,
    TeacherId     UNIQUEIDENTIFIER NOT NULL,
    QuestionId    UNIQUEIDENTIFIER NULL,
    QuestionType  NVARCHAR(20)     NOT NULL,
    HasImages     BIT              NOT NULL DEFAULT 0,
    TempImagePath NVARCHAR(500)    NULL,
    Status        NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
    FailureReason NVARCHAR(500)    NULL,
    AttemptCount  INT              NOT NULL DEFAULT 0,
    CreatedAt     NVARCHAR(19)     NOT NULL,
    CompletedAt   NVARCHAR(19)     NULL,
    CONSTRAINT PK_QuestionJob PRIMARY KEY (Id)
);

-- PART 5: Indexes
--CREATE INDEX IX_Topic_SubjectId          ON Topic(SubjectId);
--CREATE INDEX IX_SubTopic_TopicId         ON SubTopic(TopicId);
--CREATE INDEX IX_Subject_SchoolId         ON Subjects(SchoolId);
--CREATE INDEX IX_Topic_SchoolId           ON Topic(SchoolId);
--CREATE INDEX IX_SubTopic_SchoolId        ON SubTopic(SchoolId);
--CREATE INDEX IX_Questions_SubTopicId     ON Questions(SubTopicId);
--CREATE INDEX IX_Questions_JobId          ON Questions(JobId);
CREATE INDEX IX_QuestionJob_Status       ON QuestionJob(Status);
CREATE INDEX IX_QuestionJob_TeacherId_Status ON QuestionJob(TeacherId, Status);
CREATE INDEX IX_QuestionJob_SchoolId     ON QuestionJob(SchoolId);
--------------------------------------------------------------------------------

ALTER TABLE Questions
ALTER COLUMN TopicId UNIQUEIDENTIFIER NULL;

ALTER TABLE Questions  
ALTER COLUMN SubjectId UNIQUEIDENTIFIER NULL;
--------------------------------------------------

ALTER TABLE Questions ADD ClassroomId UNIQUEIDENTIFIER NULL;
ALTER TABLE Questions ADD TopicId     UNIQUEIDENTIFIER NULL;
ALTER TABLE Questions ADD SubTopicId  UNIQUEIDENTIFIER NULL;

-- Indexes for the new filter columns
CREATE INDEX IX_Questions_ClassroomId 
    ON Questions (SchoolId, ClassroomId);

CREATE INDEX IX_Questions_SubTopicId  
    ON Questions (SchoolId, SubTopicId);

CREATE INDEX IX_Questions_TopicId     
    ON Questions (SchoolId, TopicId);

--------------------------------------------

ALTER TABLE QuestionJob ADD ClassroomId UNIQUEIDENTIFIER NULL;
ALTER TABLE QuestionJob ADD SubjectId   UNIQUEIDENTIFIER NULL;
ALTER TABLE QuestionJob ADD SubTopicId  UNIQUEIDENTIFIER NULL;

