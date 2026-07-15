-- =====================================================
-- Fix Assessment System — create missing tables
-- Run this against your database if tables are missing
-- =====================================================

-- 1. AssessmentConfig
IF OBJECT_ID('AssessmentConfig', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentConfig (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AssessmentId        UNIQUEIDENTIFIER NOT NULL,
        TimeLimitMinutes    INT              NOT NULL DEFAULT 0,
        ShuffleQuestions    BIT              NOT NULL DEFAULT 0,
        PassMarkPercent     INT              NOT NULL DEFAULT 50,
        ShowResultImmediately BIT            NOT NULL DEFAULT 1,
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
END;

-- 2. AssessmentQuestion
IF OBJECT_ID('AssessmentQuestion', 'U') IS NULL
BEGIN
    CREATE TABLE AssessmentQuestion (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AssessmentId UNIQUEIDENTIFIER NOT NULL,
        QuestionId   UNIQUEIDENTIFIER NOT NULL,
        SchoolId     UNIQUEIDENTIFIER NOT NULL,
        SubTopicId   UNIQUEIDENTIFIER NULL,
        DisplayOrder INT              NOT NULL DEFAULT 0,
        CreatedAt    NVARCHAR(30)     NOT NULL,
        IsActive     BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_AssessmentQuestion_Assessment
            FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );

    CREATE INDEX IX_AssessmentQuestion_AssessmentId ON AssessmentQuestion(AssessmentId);
END;

-- 3. AssessmentAssignment
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
        CONSTRAINT FK_AssessmentAssignment_Assessment
            FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );

    CREATE INDEX IX_AssessmentAssignment_Target
        ON AssessmentAssignment(TargetType, TargetId, IsActive)
        INCLUDE (AssessmentId);
END;

-- 4. AssessmentAttempt
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
        CONSTRAINT FK_AssessmentAttempt_Assessment
            FOREIGN KEY (AssessmentId) REFERENCES Assessment(Id)
    );

    CREATE INDEX IX_AssessmentAttempt_Student
        ON AssessmentAttempt(StudentId, AssessmentId, IsOfficial)
        INCLUDE (Status, FinalScorePercent);
END;

-- 5. AssessmentAttemptAnswer
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

    CREATE INDEX IX_AssessmentAttemptAnswer_AttemptId
        ON AssessmentAttemptAnswer(AttemptId);
END;

PRINT 'Assessment system tables created successfully.';
