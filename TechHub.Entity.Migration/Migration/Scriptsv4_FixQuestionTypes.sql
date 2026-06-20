-- ============================================================
-- Fix Question table for Essay / Board / TrueFalse support
-- Run this AFTER Scriptsv2.sql (which added CorrectAnswer)
-- ============================================================

-- 1. Widen CorrectAnswer for essay / short answer model answers
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Questions' AND COLUMN_NAME = 'CorrectAnswer'
)
BEGIN
    ALTER TABLE Questions
    ALTER COLUMN CorrectAnswer NVARCHAR(MAX) NULL;
    PRINT 'CorrectAnswer widened to NVARCHAR(MAX)';
END
ELSE
BEGIN
    ALTER TABLE Questions
    ADD CorrectAnswer NVARCHAR(MAX) NULL;
    PRINT 'CorrectAnswer added as NVARCHAR(MAX)';
END

-- 2. Ensure SnapshotUrl column exists (for board questions created in one call)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Questions' AND COLUMN_NAME = 'SnapshotUrl'
)
BEGIN
    ALTER TABLE Questions
    ADD SnapshotUrl NVARCHAR(500) NULL;
    PRINT 'SnapshotUrl added';
END

-- 3. Ensure SnapshotPublicId column exists
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Questions' AND COLUMN_NAME = 'SnapshotPublicId'
)
BEGIN
    ALTER TABLE Questions
    ADD SnapshotPublicId NVARCHAR(200) NULL;
    PRINT 'SnapshotPublicId added';
END
