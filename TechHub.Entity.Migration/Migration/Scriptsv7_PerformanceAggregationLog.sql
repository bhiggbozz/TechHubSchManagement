-- ============================================================
-- Scriptsv7_PerformanceAggregationLog.sql
-- Creates job-tracking table for the performance aggregation
-- background worker. Each school's aggregation cycle gets a row.
--
-- Developers can query this table to see:
--   - Whether the last aggregation succeeded or failed
--   - Error messages when it failed (for debugging)
--   - How many times a school has been retried
--   - How many MongoDB documents were upserted per run
-- ============================================================

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PerformanceAggregationLog')
BEGIN
    CREATE TABLE PerformanceAggregationLog (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        SchoolId        UNIQUEIDENTIFIER NOT NULL,
        RunStartedAt    NVARCHAR(20) NOT NULL,
        RunCompletedAt  NVARCHAR(20) NULL,
        Status          NVARCHAR(20) NOT NULL DEFAULT 'Running',
        ErrorMessage    NVARCHAR(MAX) NULL,
        AttemptNumber   INT NOT NULL DEFAULT 1,
        ItemsUpserted   INT NULL,
        CreationDate    NVARCHAR(20) NOT NULL DEFAULT CONVERT(NVARCHAR(20), GETUTCDATE(), 120)
    );

    CREATE NONCLUSTERED INDEX IX_PerformanceAggregationLog_SchoolId_Status
        ON PerformanceAggregationLog (SchoolId, Status);

    PRINT 'Table PerformanceAggregationLog created successfully.';
END
ELSE
BEGIN
    PRINT 'Table PerformanceAggregationLog already exists. Skipping.';
END
