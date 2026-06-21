-- ============================================================
-- BlueHub AssessmentSet Migration (v3)
-- ============================================================

-- 0. Migrate QuizConfig old StarMarkConfig → typed columns
--    Add columns if missing; drop old JSON column if present
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'EasyMarks')
    ALTER TABLE QuizConfig ADD EasyMarks INT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'MediumMarks')
    ALTER TABLE QuizConfig ADD MediumMarks INT NOT NULL DEFAULT 2;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'HardMarks')
    ALTER TABLE QuizConfig ADD HardMarks INT NOT NULL DEFAULT 3;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'ExamLevelMarks')
    ALTER TABLE QuizConfig ADD ExamLevelMarks INT NOT NULL DEFAULT 5;
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'StarMarkConfig')
    ALTER TABLE QuizConfig DROP COLUMN StarMarkConfig;

-- 1. Create AssessmentSet table
CREATE TABLE AssessmentSet (
    Id                   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    Name                 NVARCHAR(100)    NOT NULL,           -- e.g. "Mid-Term Strict"
    Label                NVARCHAR(50)     NOT NULL DEFAULT 'Quiz', -- Student sees: Quiz/Assessment/Test/Exam
    TeacherId            UNIQUEIDENTIFIER NOT NULL,
    SchoolId             UNIQUEIDENTIFIER NOT NULL,
    AllowRetakes         BIT              NOT NULL DEFAULT 0,
    MaxAttempts          INT              NOT NULL DEFAULT 1,
    PassMarkPercent      INT              NOT NULL DEFAULT 50,
    TimeLimitMinutes     INT              NULL,
    AutoSubmitOnTimeout  BIT              NOT NULL DEFAULT 1,
    ShuffleQuestions     BIT              NOT NULL DEFAULT 0,
    ShowResultMode       NVARCHAR(20)     NOT NULL DEFAULT 'Immediate', -- Immediate | AfterAllSubmit | Manual
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

-- 2. Add AssessmentSetId to LessonContent
ALTER TABLE LessonContent
ADD AssessmentSetId UNIQUEIDENTIFIER NULL;

ALTER TABLE LessonContent
ADD CONSTRAINT FK_LessonContent_AssessmentSet
    FOREIGN KEY (AssessmentSetId) REFERENCES AssessmentSet(Id);

CREATE INDEX IX_LessonContent_AssessmentSet ON LessonContent(AssessmentSetId);

-- 3. Add DefaultAssessmentSetId to QuizConfig
ALTER TABLE QuizConfig
ADD DefaultAssessmentSetId UNIQUEIDENTIFIER NULL;

ALTER TABLE QuizConfig
ADD CONSTRAINT FK_QuizConfig_DefaultAssessmentSet
    FOREIGN KEY (DefaultAssessmentSetId) REFERENCES AssessmentSet(Id);

-- 4. Migrate existing QuizConfig → AssessmentSet (as "Default Settings")
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
FROM QuizConfig 
WHERE IsActive = 1;

-- 5. Point QuizConfig.DefaultAssessmentSetId to new rows
UPDATE q 
SET DefaultAssessmentSetId = a.Id
FROM QuizConfig q
INNER JOIN AssessmentSet a ON a.TeacherId = q.TeacherId AND a.SchoolId = q.SchoolId;

-- 6. Point existing LessonContent rows to defaults
UPDATE l 
SET AssessmentSetId = q.DefaultAssessmentSetId
FROM LessonContent l
INNER JOIN QuizConfig q ON q.TeacherId = l.CreatedBy
WHERE l.QuizCode IS NOT NULL AND l.AssessmentSetId IS NULL;
