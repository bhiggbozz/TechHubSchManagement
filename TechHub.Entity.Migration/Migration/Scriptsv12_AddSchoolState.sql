-- =====================================================
-- Add State (name) column to School table
-- Stores the state name e.g. "Lagos", "New York"
-- StateId (int) remains unchanged
-- =====================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'School' AND COLUMN_NAME = 'State'
)
BEGIN
    ALTER TABLE School
    ADD State NVARCHAR(100) NULL;
END
