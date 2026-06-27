-- ============================================================
-- Quiz AssessmentSetId + QuizConfig marks columns
-- ============================================================

-- ── 1. Add AssessmentSetId to Quiz table ───────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Quiz' AND COLUMN_NAME = 'AssessmentSetId'
)
BEGIN
    ALTER TABLE Quiz ADD AssessmentSetId UNIQUEIDENTIFIER NULL;
END

-- ── 2. Add marks columns to QuizConfig if missing ──────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'EasyMarks'
)
    ALTER TABLE QuizConfig ADD EasyMarks INT NOT NULL DEFAULT 1;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'MediumMarks'
)
    ALTER TABLE QuizConfig ADD MediumMarks INT NOT NULL DEFAULT 2;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'HardMarks'
)
    ALTER TABLE QuizConfig ADD HardMarks INT NOT NULL DEFAULT 3;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'ExamLevelMarks'
)
    ALTER TABLE QuizConfig ADD ExamLevelMarks INT NOT NULL DEFAULT 5;

-- ── 3. Add DefaultAssessmentSetId to QuizConfig if missing ─────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'QuizConfig' AND COLUMN_NAME = 'DefaultAssessmentSetId'
)
    ALTER TABLE QuizConfig ADD DefaultAssessmentSetId UNIQUEIDENTIFIER NULL;
