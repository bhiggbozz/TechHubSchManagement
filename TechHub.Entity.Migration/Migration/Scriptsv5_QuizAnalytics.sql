-- ============================================================
-- BlueHub Quiz Analytics Tables
-- Pre-computed aggregates for fast admin dashboard queries
-- Background job refreshes these every 3 hours + EOD
-- ============================================================

-- ── 1. QuizAnalyticsSummary (per-lesson snapshot) ─────────────────────────
IF OBJECT_ID('dbo.QuizAnalyticsSummary', 'U') IS NULL
BEGIN
    CREATE TABLE QuizAnalyticsSummary (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        LessonId            UNIQUEIDENTIFIER NOT NULL,
        SchoolId            UNIQUEIDENTIFIER NOT NULL,
        QuizCode            NVARCHAR(20)     NOT NULL,

        -- Participation
        TotalStudents       INT              NOT NULL DEFAULT 0,
        AttemptedCount      INT              NOT NULL DEFAULT 0,
        CompletedCount      INT              NOT NULL DEFAULT 0,
        ParticipationRate   DECIMAL(5,2)     NOT NULL DEFAULT 0, -- % of enrolled who attempted

        -- Performance
        AverageScore        DECIMAL(5,2)     NULL,
        HighestScore        DECIMAL(5,2)     NULL,
        LowestScore         DECIMAL(5,2)     NULL,
        PassRate            DECIMAL(5,2)     NULL, -- % who passed
        AverageTimeSeconds  INT              NULL,

        -- Breakdown
        AutoGradedCount     INT              NOT NULL DEFAULT 0,
        PartiallyGradedCount INT             NOT NULL DEFAULT 0,
        FullyGradedCount    INT              NOT NULL DEFAULT 0,
        PendingGradingCount INT              NOT NULL DEFAULT 0,

        -- Timestamps
        ComputedAt          NVARCHAR(30)     NOT NULL,
        PeriodStart         NVARCHAR(30)     NOT NULL, -- inclusive
        PeriodEnd           NVARCHAR(30)     NOT NULL, -- inclusive

        CONSTRAINT UQ_QuizAnalyticsSummary_Lesson_Period UNIQUE (LessonId, PeriodStart, PeriodEnd)
    );

    CREATE INDEX IX_QuizAnalyticsSummary_School ON QuizAnalyticsSummary(SchoolId, ComputedAt DESC);
    CREATE INDEX IX_QuizAnalyticsSummary_Lesson ON QuizAnalyticsSummary(LessonId);
END

-- ── 2. QuizQuestionAnalytics (per-question per-lesson stats) ──────────────
IF OBJECT_ID('dbo.QuizQuestionAnalytics', 'U') IS NULL
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
        SuccessRate         DECIMAL(5,2)     NOT NULL DEFAULT 0, -- % who got full marks
        AvgTimeTakenMs      BIGINT           NULL,

        ComputedAt          NVARCHAR(30)     NOT NULL,

        CONSTRAINT UQ_QuizQuestionAnalytics_Lesson_Question UNIQUE (LessonId, QuestionId)
    );

    CREATE INDEX IX_QuizQuestionAnalytics_Lesson ON QuizQuestionAnalytics(LessonId);
END

-- ── 3. QuizStudentAnalytics (per-student per-lesson stats) ────────────────
IF OBJECT_ID('dbo.QuizStudentAnalytics', 'U') IS NULL
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

    CREATE INDEX IX_QuizStudentAnalytics_Lesson ON QuizStudentAnalytics(LessonId);
    CREATE INDEX IX_QuizStudentAnalytics_Student ON QuizStudentAnalytics(StudentId);
END
