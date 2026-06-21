-- ============================================================
-- BlueHub Quiz System Base Tables
-- Run this if you ONLY have Quiz + QuizQuestion tables
-- ============================================================

-- ── 1. QuizConfig (per-teacher default settings) ───────────────────────────
IF OBJECT_ID('dbo.QuizConfig', 'U') IS NULL
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
        IsActive                  BIT              NOT NULL DEFAULT 1
    );

    CREATE INDEX IX_QuizConfig_Teacher ON QuizConfig(TeacherId, SchoolId, IsActive);
END

-- ── 2. AssessmentSet (reusable frozen snapshots) ──────────────────────────
IF OBJECT_ID('dbo.AssessmentSet', 'U') IS NULL
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
END

-- ── 3. Add AssessmentSetId to QuizConfig (FK) ─────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'DefaultAssessmentSetId'
)
BEGIN
    ALTER TABLE QuizConfig ADD DefaultAssessmentSetId UNIQUEIDENTIFIER NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE TABLE_NAME = 'QuizConfig' AND CONSTRAINT_NAME = 'FK_QuizConfig_DefaultAssessmentSet'
)
BEGIN
    ALTER TABLE QuizConfig
    ADD CONSTRAINT FK_QuizConfig_DefaultAssessmentSet
        FOREIGN KEY (DefaultAssessmentSetId) REFERENCES AssessmentSet(Id);
END

-- ── 4. Add AssessmentSetId to LessonContent (FK) ─────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'LessonContent' AND COLUMN_NAME = 'AssessmentSetId'
)
BEGIN
    ALTER TABLE LessonContent ADD AssessmentSetId UNIQUEIDENTIFIER NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE TABLE_NAME = 'LessonContent' AND CONSTRAINT_NAME = 'FK_LessonContent_AssessmentSet'
)
BEGIN
    ALTER TABLE LessonContent
    ADD CONSTRAINT FK_LessonContent_AssessmentSet
        FOREIGN KEY (AssessmentSetId) REFERENCES AssessmentSet(Id);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_LessonContent_AssessmentSet' AND object_id = OBJECT_ID('LessonContent')
)
BEGIN
    CREATE INDEX IX_LessonContent_AssessmentSet ON LessonContent(AssessmentSetId);
END

-- ── 5. QuizAttempt (student quiz attempt header) ──────────────────────────
IF OBJECT_ID('dbo.QuizAttempt', 'U') IS NULL
BEGIN
    CREATE TABLE QuizAttempt (
        Id                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        QuizCode          NVARCHAR(20)     NOT NULL,
        LessonId          UNIQUEIDENTIFIER NOT NULL,
        StudentId         UNIQUEIDENTIFIER NOT NULL,
        SchoolId          UNIQUEIDENTIFIER NOT NULL,
        AttemptNumber     INT              NOT NULL DEFAULT 1,
        StartedAt         NVARCHAR(30)     NOT NULL,
        SubmittedAt       NVARCHAR(30)     NULL,
        TimeTakenSeconds  INT              NULL,
        TotalQuestions    INT              NOT NULL DEFAULT 0,
        TotalAutoGraded   INT              NOT NULL DEFAULT 0,
        TotalManualGraded INT              NOT NULL DEFAULT 0,
        TotalCorrect      INT              NOT NULL DEFAULT 0,
        TotalWrong        INT              NOT NULL DEFAULT 0,
        TotalSkipped      INT              NOT NULL DEFAULT 0,
        AutoMarksObtained DECIMAL(10,2)    NOT NULL DEFAULT 0,
        ManualMarksObtained DECIMAL(10,2)  NOT NULL DEFAULT 0,
        TotalMarks        DECIMAL(10,2)    NOT NULL DEFAULT 0,
        FinalScorePercent DECIMAL(5,2)     NULL,
        IsPassed          BIT              NULL,
        Status            NVARCHAR(20)     NOT NULL DEFAULT 'InProgress',
        CreationDate      NVARCHAR(30)     NOT NULL,
        ModifiedDate      NVARCHAR(30)     NOT NULL
    );

    CREATE INDEX IX_QuizAttempt_Student ON QuizAttempt(StudentId, LessonId, SchoolId);
    CREATE INDEX IX_QuizAttempt_Status   ON QuizAttempt(Status, SchoolId);
    CREATE INDEX IX_QuizAttempt_Lesson   ON QuizAttempt(LessonId, SchoolId, Status);
END

-- ── 6. QuizAttemptAnswer (individual answers per attempt) ─────────────────
IF OBJECT_ID('dbo.QuizAttemptAnswer', 'U') IS NULL
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
END

-- ── 7. QuizAttemptAssistance (AI help log — reserved for future) ──────────
IF OBJECT_ID('dbo.QuizAttemptAssistance', 'U') IS NULL
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
END

-- ── 8. Seed defaults: create one AssessmentSet per existing QuizConfig ────
--    (safe to re-run; only creates if rows are missing)
INSERT INTO AssessmentSet (Name, Label, TeacherId, SchoolId, AllowRetakes, MaxAttempts,
    PassMarkPercent, TimeLimitMinutes, AutoSubmitOnTimeout, ShuffleQuestions,
    ShowResultMode, ShowCorrectAnswers, AllowBoardAnswer,
    EasyMarks, MediumMarks, HardMarks, ExamLevelMarks,
    IsActive, CreationDate, ModifiedDate)
SELECT
    'Default Settings',
    'Quiz',
    TeacherId,
    SchoolId,
    AllowRetakes,
    MaxAttempts,
    PassMarkPercent,
    TimeLimitMinutes,
    AutoSubmitOnTimeout,
    ShuffleQuestions,
    'Immediate',
    ShowCorrectAnswers,
    AllowBoardAnswer,
    EasyMarks,
    MediumMarks,
    HardMarks,
    ExamLevelMarks,
    IsActive,
    CreationDate,
    ModifiedDate
FROM QuizConfig qc
WHERE IsActive = 1
  AND NOT EXISTS (
      SELECT 1 FROM AssessmentSet a
      WHERE a.TeacherId = qc.TeacherId AND a.SchoolId = qc.SchoolId
  );

-- ── 9. Link QuizConfig.DefaultAssessmentSetId to new AssessmentSets ───────
UPDATE q
SET DefaultAssessmentSetId = a.Id
FROM QuizConfig q
INNER JOIN AssessmentSet a ON a.TeacherId = q.TeacherId AND a.SchoolId = q.SchoolId
WHERE q.DefaultAssessmentSetId IS NULL;

-- ── 10. Link existing LessonContent rows to default AssessmentSets ───────
UPDATE l
SET AssessmentSetId = q.DefaultAssessmentSetId
FROM LessonContent l
INNER JOIN QuizConfig q ON q.TeacherId = l.CreatedBy
WHERE l.QuizCode IS NOT NULL
  AND l.AssessmentSetId IS NULL;
