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

