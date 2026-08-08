-- ========================================================================
-- SCRIPT v15: CLASS PREPARATION APPROVAL COLUMNS + INDEXES
-- ========================================================================
-- Backfills columns defined in Scripts.sql / Script_Initial.sql that were
-- never applied to this database:
--   1. ClassPreparation.ApprovalNotes
--   2. ClassPreparation.ApprovalTimeMinutes
--   3. ClassPreparation.IsUrgent
--   4. ClassPreparation.NeedsReview
--   5. ClassPreparation indexes (Status/IsUrgent + AutoApprovalEligible)
-- ========================================================================

IF COL_LENGTH('ClassPreparation', 'ApprovalNotes') IS NULL
BEGIN
    ALTER TABLE ClassPreparation ADD ApprovalNotes NVARCHAR(1000) NULL;
END
GO

IF COL_LENGTH('ClassPreparation', 'ApprovalTimeMinutes') IS NULL
BEGIN
    ALTER TABLE ClassPreparation ADD ApprovalTimeMinutes INT NULL;
END
GO

IF COL_LENGTH('ClassPreparation', 'IsUrgent') IS NULL
BEGIN
    ALTER TABLE ClassPreparation ADD IsUrgent BIT NOT NULL DEFAULT 0;
END
GO

IF COL_LENGTH('ClassPreparation', 'NeedsReview') IS NULL
BEGIN
    ALTER TABLE ClassPreparation ADD NeedsReview BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassPreparation_Status_Urgent' AND object_id = OBJECT_ID('ClassPreparation'))
BEGIN
    CREATE INDEX IX_ClassPreparation_Status_Urgent
        ON ClassPreparation(Status, IsUrgent DESC, SubmittedForApprovalDate DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassPreparation_AutoApprovalEligible' AND object_id = OBJECT_ID('ClassPreparation'))
BEGIN
    CREATE INDEX IX_ClassPreparation_AutoApprovalEligible
        ON ClassPreparation(AutoApprovalEligible, Status)
        WHERE AutoApprovalEligible = 1 AND Status = 1;
END
GO

PRINT 'Script v15 (ClassPreparation approval columns) applied successfully.';
