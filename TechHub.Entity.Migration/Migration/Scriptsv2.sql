CREATE TABLE Topic (
    Id        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    SubjectId UNIQUEIDENTIFIER NOT NULL,
    SchoolId  UNIQUEIDENTIFIER NOT NULL,
    Name      NVARCHAR(200)    NOT NULL,
    IsActive  BIT              NOT NULL DEFAULT 1,
    IsDeleted BIT              NOT NULL DEFAULT 0,
    CreatedAt NVARCHAR(19)     NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_Topic         PRIMARY KEY (Id),
    CONSTRAINT FK_Topic_Subject FOREIGN KEY (SubjectId) REFERENCES Subject(Id)
);
 
CREATE TABLE SubTopic (
    Id        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    TopicId   UNIQUEIDENTIFIER NOT NULL,
    SchoolId  UNIQUEIDENTIFIER NOT NULL,
    Name      NVARCHAR(200)    NOT NULL,
    IsActive  BIT              NOT NULL DEFAULT 1,
    IsDeleted BIT              NOT NULL DEFAULT 0,
    CreatedAt NVARCHAR(19)     NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_SubTopic        PRIMARY KEY (Id),
    CONSTRAINT FK_SubTopic_Topic  FOREIGN KEY (TopicId) REFERENCES Topic(Id)
);
 
CREATE INDEX IX_Topic_SubjectId    ON Topic(SubjectId);
CREATE INDEX IX_SubTopic_TopicId   ON SubTopic(TopicId);
CREATE INDEX IX_Subject_SchoolId   ON Subject(SchoolId);
CREATE INDEX IX_Topic_SchoolId     ON Topic(SchoolId);
CREATE INDEX IX_SubTopic_SchoolId  ON SubTopic(SchoolId);
 
 
-- =====================================================
-- PART 2: ALTER QUESTIONS TABLE
-- Add new columns for AI pipeline
-- Existing columns untouched — backward compatible
-- =====================================================
 
ALTER TABLE Questions
ADD SubTopicId      UNIQUEIDENTIFIER NULL,
    QuestionHtml    NVARCHAR(MAX)    NULL,
    ContentParts    NVARCHAR(MAX)    NULL,
    HasLatex        BIT              NOT NULL DEFAULT 0,
    JobId           UNIQUEIDENTIFIER NULL,
    CorrectAnswer   NVARCHAR(10)     NULL;
-- CorrectAnswer: used for TrueFalse only ("True" / "False")
-- SubTopicId: FK to SubTopic table — new questions use this
-- Topic + SubTopic strings kept for existing questions
 
CREATE INDEX IX_Questions_SubTopicId ON Questions(SubTopicId);
CREATE INDEX IX_Questions_JobId       ON Questions(JobId);
 
 
-- =====================================================
-- PART 3: ALTER QUESTIONOPTIONS TABLE
-- Add rich content columns for LaTeX / image options
-- =====================================================
 
ALTER TABLE QuestionOptions
ADD OptionHtml    NVARCHAR(MAX) NULL,
    ContentParts  NVARCHAR(MAX) NULL,
    HasLatex      BIT           NOT NULL DEFAULT 0,
    HasImages     BIT           NOT NULL DEFAULT 0;
 
 
-- =====================================================
-- PART 4: QUESTIONJOB TABLE
-- Async upload pipeline job tracker
-- One row per teacher upload
-- =====================================================
 
CREATE TABLE QuestionJob (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    SchoolId        UNIQUEIDENTIFIER NOT NULL,
    SubTopicId      UNIQUEIDENTIFIER NOT NULL,
    TeacherId       UNIQUEIDENTIFIER NOT NULL,
    QuestionId      UNIQUEIDENTIFIER NULL,
    -- NULL until background worker completes successfully
 
    QuestionType    NVARCHAR(20)     NOT NULL,
    -- Objective | Theory | TrueFalse
 
    HasImages       BIT              NOT NULL DEFAULT 0,
    -- Teacher declared upfront — drives upload pipeline
 
    TempImagePath   NVARCHAR(500)    NULL,
    -- Cloudinary temp folder path
    -- Background worker reads from here
    -- Deleted after processing complete
 
    Status          NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
    -- Pending | Processing | Completed | Failed
 
    FailureReason   NVARCHAR(500)    NULL,
    -- Populated when Status = Failed
    -- Shown to teacher so they know why
 
    AttemptCount    INT              NOT NULL DEFAULT 0,
    -- Background worker increments on each attempt
    -- Max 3 attempts before marking permanently Failed
 
    CreatedAt       NVARCHAR(19)     NOT NULL,
    CompletedAt     NVARCHAR(19)     NULL,
 
    CONSTRAINT PK_QuestionJob PRIMARY KEY (Id),
    CONSTRAINT FK_QuestionJob_SubTopic
        FOREIGN KEY (SubTopicId) REFERENCES SubTopic(Id)
);
 
CREATE INDEX IX_QuestionJob_TeacherId_Status ON QuestionJob(TeacherId, Status);
-- Teacher polls their own jobs by status
 
CREATE INDEX IX_QuestionJob_Status ON QuestionJob(Status);
-- Background worker picks up Pending jobs
 
CREATE INDEX IX_QuestionJob_SchoolId ON QuestionJob(SchoolId);

----------------------------------------------------------------------

CREATE TABLE RefreshTokens (
    Id            UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    UserId        UNIQUEIDENTIFIER  NOT NULL,
    SchoolId      UNIQUEIDENTIFIER  NOT NULL,
    Token         NVARCHAR(500)     NOT NULL,
    ExpiresAt     DATETIME2         NOT NULL,
    CreatedAt     DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    RevokedAt     DATETIME2         NULL,
    ReplacedByToken NVARCHAR(500)   NULL,   -- for rotation tracking
    IsRevoked     BIT               NOT NULL DEFAULT 0,

    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
        REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_RefreshTokens_Token  ON RefreshTokens (Token);
CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens (UserId);
 