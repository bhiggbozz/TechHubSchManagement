-- =====================================================
-- Assessment System
-- Replica of Quiz but with flexible assignment model
-- Can be assigned to: individual Student, Subject, or Classroom
-- First attempt = official score; retries recorded but not counted
-- =====================================================

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

CREATE TABLE AssessmentConfig (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    AssessmentId        UNIQUEIDENTIFIER NOT NULL,
    TimeLimitMinutes    INT              NOT NULL,
    ShuffleQuestions    BIT              NOT NULL DEFAULT 0,
    PassMarkPercent     INT              NOT NULL DEFAULT 50,
    ShowResultImmediately BIT           NOT NULL DEFAULT 1,
    EasyMarks           INT              NOT NULL DEFAULT 1,
    MediumMarks         INT              NOT NULL DEFAULT 2,
    HardMarks           INT              NOT NULL DEFAULT 3,
    ExamLevelMarks      INT              NOT NULL DEFAULT 5,
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    CreationDate        NVARCHAR(30)     NOT NULL,
    ModifiedDate        NVARCHAR(30)     NOT NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    CONSTRAINT FK_AssessmentConfig_Assessment
        FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
);

CREATE TABLE AssessmentQuestion (
    Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    AssessmentId UNIQUEIDENTIFIER NOT NULL,
    QuestionId   UNIQUEIDENTIFIER NOT NULL,
    SchoolId     UNIQUEIDENTIFIER NOT NULL,
    DisplayOrder INT              NOT NULL DEFAULT 0,
    CreationDate NVARCHAR(30)     NOT NULL,
    IsActive     BIT              NOT NULL DEFAULT 1,
    CONSTRAINT FK_AssessmentQuestion_Assessment
        FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
);

CREATE INDEX IX_AssessmentQuestion_AssessmentId
    ON AssessmentQuestion(AssessmentId);

-- TargetType: 'Student' | 'Subject' | 'Classroom'
CREATE TABLE AssessmentAssignment (
    Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    AssessmentId UNIQUEIDENTIFIER NOT NULL,
    TargetType   NVARCHAR(20)     NOT NULL,
    TargetId     UNIQUEIDENTIFIER NOT NULL,
    SchoolId     UNIQUEIDENTIFIER NOT NULL,
    CreatedBy    UNIQUEIDENTIFIER NOT NULL,
    CreationDate NVARCHAR(30)     NOT NULL,
    IsActive     BIT              NOT NULL DEFAULT 1,
    CONSTRAINT FK_AssessmentAssignment_Assessment
        FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
);

CREATE INDEX IX_AssessmentAssignment_Target
    ON AssessmentAssignment(TargetType, TargetId, IsActive)
    INCLUDE (AssessmentId);

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
    CONSTRAINT FK_AssessmentAttempt_Assessment
        FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
);

CREATE INDEX IX_AssessmentAttempt_Student
    ON AssessmentAttempt(StudentId, AssessmentId, IsOfficial)
    INCLUDE (Status, FinalScorePercent);

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

CREATE INDEX IX_AssessmentAttemptAnswer_AttemptId
    ON AssessmentAttemptAnswer(AttemptId);
