-- ========================================================================
-- SCRIPT v14: SCHOOL AUDIT COLUMNS
-- ========================================================================
-- Adds:
--   1. School.CreatedBy            - platform user who created the school
--   2. School.ModifiedBy           - platform user who last edited the school
-- ========================================================================

IF COL_LENGTH('School', 'CreatedBy') IS NULL
BEGIN
    ALTER TABLE School ADD CreatedBy UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('School', 'ModifiedBy') IS NULL
BEGIN
    ALTER TABLE School ADD ModifiedBy UNIQUEIDENTIFIER NULL;
END
GO

PRINT 'Script v14 (School audit columns) applied successfully.';
