-- ============================================================
-- Quiz AssessmentSetId
-- Allows a quiz to carry its own config reference (AssessmentSet)
-- instead of always resolving from the lesson.
-- ============================================================

-- ── 1. Add AssessmentSetId to Quiz table ───────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Quiz' AND COLUMN_NAME = 'AssessmentSetId'
)
BEGIN
    ALTER TABLE Quiz ADD AssessmentSetId UNIQUEIDENTIFIER NULL;
END
