-- =====================================================
-- Add expiry and answer-visibility fields to AssessmentConfig
-- =====================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'AssessmentConfig' AND COLUMN_NAME = 'ExpiresAt'
)
BEGIN
    ALTER TABLE AssessmentConfig ADD ExpiresAt DATETIME NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'AssessmentConfig' AND COLUMN_NAME = 'ShowCorrectAnswers'
)
BEGIN
    ALTER TABLE AssessmentConfig ADD ShowCorrectAnswers BIT NOT NULL DEFAULT 0;
END;

PRINT 'AssessmentConfig columns updated.';
