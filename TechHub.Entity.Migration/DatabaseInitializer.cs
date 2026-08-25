using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Reflection;

namespace TechHub.Entity.Migration;

public class DatabaseInitializer : IHostedService
{
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connStr = _configuration.GetConnectionString("DbConnectionString");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Log.Warning("DatabaseInitializer: DbConnectionString not found. Skipping migration.");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "TechHub.Entity.Migration.Migration.Script_Initial.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            Log.Error("DatabaseInitializer: Embedded resource '{Resource}' not found.", resourceName);
            return;
        }

        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync();

        // Split by GO statements (SQL Server batch separator)
        var batches = script.Split(
            new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n", "\r\nGO", "\nGO", "GO\r\n", "GO\n" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        Log.Information("DatabaseInitializer: Running initial migration ({BatchCount} batches)...", batches.Length);

        try
        {
            await using var connection = new SqlConnection(connStr);
            await connection.OpenAsync(cancellationToken);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;

                await using var cmd = new SqlCommand(batch, connection);
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            Log.Information("DatabaseInitializer: Initial migration completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DatabaseInitializer: Initial migration script failed (non-fatal, continuing startup).");
        }

        // Schema migrations — run regardless of whether the main script succeeded
        try
        {
            var schemaMigrations = new[]
            {
                "IF OBJECT_ID('LessonContent', 'U') IS NOT NULL AND COL_LENGTH('LessonContent', 'IsActive') IS NULL ALTER TABLE LessonContent ADD IsActive BIT NOT NULL DEFAULT 1",
                "IF OBJECT_ID('LessonContent', 'U') IS NOT NULL AND COL_LENGTH('LessonContent', 'ShouldGenerateImage') IS NULL ALTER TABLE LessonContent ADD ShouldGenerateImage BIT NOT NULL DEFAULT 1",
                "IF OBJECT_ID('LessonContent', 'U') IS NOT NULL AND COL_LENGTH('LessonContent', 'ImageMaterialWords') IS NULL ALTER TABLE LessonContent ADD ImageMaterialWords NVARCHAR(2000) NULL",
                "IF OBJECT_ID('LessonContent', 'U') IS NOT NULL AND COL_LENGTH('LessonContent', 'ImageCount') IS NULL ALTER TABLE LessonContent ADD ImageCount INT NOT NULL DEFAULT 1",
                "IF OBJECT_ID('ClassPreparation', 'U') IS NOT NULL AND COL_LENGTH('ClassPreparation', 'AutoApprovalEligible') IS NULL ALTER TABLE ClassPreparation ADD AutoApprovalEligible BIT NOT NULL DEFAULT 0",
                "IF OBJECT_ID('ClassPreparation', 'U') IS NOT NULL AND COL_LENGTH('ClassPreparation', 'IsActive') IS NULL ALTER TABLE ClassPreparation ADD IsActive BIT DEFAULT 1",
                "ALTER TABLE AssessmentQuestion ADD SubTopicId UNIQUEIDENTIFIER NULL",
                "ALTER TABLE School ADD State NVARCHAR(100) NULL",
                "IF OBJECT_ID('SchoolFeature', 'U') IS NULL CREATE TABLE SchoolFeature (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), SchoolId UNIQUEIDENTIFIER NOT NULL, FeatureKey NVARCHAR(100) NOT NULL, IsEnabled BIT NOT NULL DEFAULT 0, ConfigurationJson NVARCHAR(MAX) NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), UpdatedAt DATETIME2 NULL, CreatedBy UNIQUEIDENTIFIER NULL, IsActive BIT NOT NULL DEFAULT 1, CONSTRAINT PK_SchoolFeature PRIMARY KEY (Id), CONSTRAINT UQ_SchoolFeature_School_Key UNIQUE (SchoolId, FeatureKey))",
                "IF OBJECT_ID('SchoolFeature', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SchoolFeature_SchoolId' AND object_id = OBJECT_ID('SchoolFeature')) CREATE INDEX IX_SchoolFeature_SchoolId ON SchoolFeature(SchoolId)",
                "IF OBJECT_ID('LessonGenerationPrompt', 'U') IS NULL CREATE TABLE LessonGenerationPrompt (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), SchoolId UNIQUEIDENTIFIER NOT NULL, LessonId UNIQUEIDENTIFIER NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL, PromptText NVARCHAR(MAX) NOT NULL, TeacherPrompt NVARCHAR(MAX) NULL, AgentType NVARCHAR(100) NOT NULL, Style NVARCHAR(200) NULL, [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending', MediaId UNIQUEIDENTIFIER NULL, ImageUrl NVARCHAR(1000) NULL, ImagePublicId NVARCHAR(500) NULL, ErrorMessage NVARCHAR(2000) NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), IsActive BIT NOT NULL DEFAULT 1, CONSTRAINT PK_LessonGenerationPrompt PRIMARY KEY (Id))",
                "IF OBJECT_ID('LessonGenerationPrompt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LessonGenerationPrompt_LessonId_SchoolId' AND object_id = OBJECT_ID('LessonGenerationPrompt')) CREATE INDEX IX_LessonGenerationPrompt_LessonId_SchoolId ON LessonGenerationPrompt(LessonId, SchoolId, CreatedAt DESC)",
                "IF OBJECT_ID('PasswordResetToken', 'U') IS NULL CREATE TABLE PasswordResetToken (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), UserId UNIQUEIDENTIFIER NOT NULL, SchoolId UNIQUEIDENTIFIER NOT NULL, Token NVARCHAR(200) NOT NULL, ExpiresAt DATETIME2 NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), IsUsed BIT NOT NULL DEFAULT 0, CONSTRAINT PK_PasswordResetToken PRIMARY KEY (Id))",
                "IF OBJECT_ID('PasswordResetToken', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PasswordResetToken_Token' AND object_id = OBJECT_ID('PasswordResetToken')) CREATE UNIQUE INDEX UQ_PasswordResetToken_Token ON PasswordResetToken(Token)",
                "IF OBJECT_ID('PasswordResetToken', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PasswordResetToken_UserId' AND object_id = OBJECT_ID('PasswordResetToken')) CREATE INDEX IX_PasswordResetToken_UserId ON PasswordResetToken(UserId)",
                "IF OBJECT_ID('StudentParent', 'U') IS NULL CREATE TABLE StudentParent (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), StudentId UNIQUEIDENTIFIER NOT NULL, ParentId UNIQUEIDENTIFIER NOT NULL, SchoolId UNIQUEIDENTIFIER NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), IsActive BIT NOT NULL DEFAULT 1, CONSTRAINT PK_StudentParent PRIMARY KEY (Id))",
                "IF OBJECT_ID('StudentParent', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_StudentParent_Student_Parent_Active' AND object_id = OBJECT_ID('StudentParent')) CREATE UNIQUE INDEX UQ_StudentParent_Student_Parent_Active ON StudentParent(StudentId, ParentId) WHERE IsActive = 1",
                "IF OBJECT_ID('StudentParent', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentParent_ParentId' AND object_id = OBJECT_ID('StudentParent')) CREATE INDEX IX_StudentParent_ParentId ON StudentParent(ParentId) WHERE IsActive = 1",
                "IF OBJECT_ID('StudentParent', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentParent_StudentId' AND object_id = OBJECT_ID('StudentParent')) CREATE INDEX IX_StudentParent_StudentId ON StudentParent(StudentId) WHERE IsActive = 1"
            };

            await using var connection = new SqlConnection(connStr);
            await connection.OpenAsync(cancellationToken);

            foreach (var migrationSql in schemaMigrations)
            {
                try
                {
                    await using var cmd = new SqlCommand(migrationSql, connection);
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    Log.Information("DatabaseInitializer: Schema migration applied: {Sql}", migrationSql);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DatabaseInitializer: Schema migration skipped (may already exist): {Sql}", migrationSql);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DatabaseInitializer: Schema migration block failed (non-fatal).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
