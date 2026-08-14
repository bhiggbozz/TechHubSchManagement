-- =====================================================
-- ApplicationLogs (database error logging)
-- Fire-and-forget, channel-based error logging.
-- Written by IDbLogger / GlobalExceptionMiddleware,
-- flushed in batches by the LogWriterService background
-- service, cleaned up daily by the Hangfire job
-- "cleanup-application-logs" (rows older than 90 days).
-- =====================================================

IF OBJECT_ID('ApplicationLogs', 'U') IS NULL
BEGIN
    CREATE TABLE ApplicationLogs (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        LogLevel      VARCHAR(20)   NOT NULL,
        Message       NVARCHAR(MAX) NOT NULL,
        Exception     NVARCHAR(MAX) NULL,
        Source        NVARCHAR(500) NULL,
        Endpoint      NVARCHAR(500) NULL,
        RequestPath   NVARCHAR(500) NULL,
        RequestMethod VARCHAR(10)   NULL,
        UserId        NVARCHAR(100) NULL,
        TenantId      NVARCHAR(100) NULL,
        CorrelationId NVARCHAR(100) NULL,
        CreatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_ApplicationLogs PRIMARY KEY (Id)
    );

    CREATE INDEX IX_Logs_CreatedAt ON ApplicationLogs(CreatedAt DESC);
    CREATE INDEX IX_Logs_Level_Date ON ApplicationLogs(LogLevel, CreatedAt DESC);
END
GO

-- Idempotent ALTER for tables created before the Endpoint column existed.
IF OBJECT_ID('ApplicationLogs', 'U') IS NOT NULL
   AND COL_LENGTH('ApplicationLogs', 'Endpoint') IS NULL
BEGIN
    ALTER TABLE ApplicationLogs ADD Endpoint NVARCHAR(500) NULL;
END
GO