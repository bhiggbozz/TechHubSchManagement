using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Reflection;
using System.Text.RegularExpressions;

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
                "IF OBJECT_ID('StudentParent', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentParent_StudentId' AND object_id = OBJECT_ID('StudentParent')) CREATE INDEX IX_StudentParent_StudentId ON StudentParent(StudentId) WHERE IsActive = 1",

                // Supporting indexes for the "My Courses" quiz/assessment ranking stored procedures below.
                "IF OBJECT_ID('AssessmentAssignment', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssessmentAssignment_AssessmentId_Active' AND object_id = OBJECT_ID('AssessmentAssignment')) CREATE INDEX IX_AssessmentAssignment_AssessmentId_Active ON AssessmentAssignment(AssessmentId, IsActive) INCLUDE (TargetType, TargetId)",
                "IF OBJECT_ID('AssessmentAttempt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssessmentAttempt_AssessmentId_Status' AND object_id = OBJECT_ID('AssessmentAttempt')) CREATE INDEX IX_AssessmentAttempt_AssessmentId_Status ON AssessmentAttempt(AssessmentId, Status) INCLUDE (StudentId, FinalScorePercent, SchoolId)",
                "IF OBJECT_ID('ClassroomSubject', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassroomSubject_SubjectId_Active' AND object_id = OBJECT_ID('ClassroomSubject')) CREATE INDEX IX_ClassroomSubject_SubjectId_Active ON ClassroomSubject(SubjectId, IsActive) INCLUDE (ClassroomId, SchoolId)",
                "IF OBJECT_ID('StudentMinorSubject', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentMinorSubject_SubjectId_Active' AND object_id = OBJECT_ID('StudentMinorSubject')) CREATE INDEX IX_StudentMinorSubject_SubjectId_Active ON StudentMinorSubject(SubjectId, IsActive) INCLUDE (StudentId, SchoolId)",

                // "My Courses" stored procedures (student self-service: subject list + per-subject quiz/assessment
                // rank, scoped to classmates for classroom-linked subjects, or other electors for minor subjects,
                // since Subjects is a flat per-school catalog shared across every classroom that teaches it).
                @"CREATE OR ALTER PROCEDURE usp_GetStudentMyCourses
                    @StudentId UNIQUEIDENTIFIER,
                    @SchoolId UNIQUEIDENTIFIER
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SELECT
                        s.Id AS SubjectId,
                        s.Subject AS SubjectName,
                        CAST(CASE WHEN EXISTS (
                            SELECT 1 FROM StudentMinorSubject sms
                            WHERE sms.StudentId = @StudentId AND sms.SubjectId = s.Id
                            AND sms.IsActive = 1 AND sms.SchoolId = @SchoolId
                        ) THEN 1 ELSE 0 END AS BIT) AS IsMinorSubject
                    FROM Subjects s
                    WHERE s.SchoolId = @SchoolId AND s.IsActive = 1
                    AND s.Id IN (
                        SELECT cs.SubjectId
                        FROM StudentClassroom sc
                        JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId AND cs.IsActive = 1 AND cs.SchoolId = @SchoolId
                        WHERE sc.StudentId = @StudentId AND sc.IsActive = 1
                        UNION
                        SELECT sms.SubjectId FROM StudentMinorSubject sms
                        WHERE sms.StudentId = @StudentId AND sms.IsActive = 1 AND sms.SchoolId = @SchoolId
                    )
                    ORDER BY s.Subject;
                END",

                @"CREATE OR ALTER PROCEDURE usp_GetStudentSubjectPerformance
                    @StudentId UNIQUEIDENTIFIER,
                    @SubjectId UNIQUEIDENTIFIER,
                    @SchoolId UNIQUEIDENTIFIER
                AS
                BEGIN
                    SET NOCOUNT ON;

                    -- Result set 1: subject name (empty = not found/wrong school) + enrollment flag
                    SELECT
                        s.Subject AS SubjectName,
                        CAST(CASE WHEN EXISTS (
                            SELECT 1 FROM ClassroomSubject cs
                            JOIN StudentClassroom sc ON sc.ClassroomId = cs.ClassroomId AND sc.IsActive = 1
                            WHERE cs.SubjectId = @SubjectId AND cs.IsActive = 1 AND cs.SchoolId = @SchoolId
                            AND sc.StudentId = @StudentId
                            UNION ALL
                            SELECT 1 FROM StudentMinorSubject sms
                            WHERE sms.SubjectId = @SubjectId AND sms.StudentId = @StudentId
                            AND sms.IsActive = 1 AND sms.SchoolId = @SchoolId
                        ) THEN 1 ELSE 0 END AS BIT) AS IsEnrolled
                    FROM Subjects s
                    WHERE s.Id = @SubjectId AND s.SchoolId = @SchoolId AND s.IsActive = 1;

                    DECLARE @ClassroomId UNIQUEIDENTIFIER;
                    SELECT TOP 1 @ClassroomId = ClassroomId FROM StudentClassroom
                    WHERE StudentId = @StudentId AND IsActive = 1;

                    DECLARE @IsCore BIT = 0;
                    IF @ClassroomId IS NOT NULL AND EXISTS (
                        SELECT 1 FROM ClassroomSubject
                        WHERE ClassroomId = @ClassroomId AND SubjectId = @SubjectId AND IsActive = 1 AND SchoolId = @SchoolId
                    )
                        SET @IsCore = 1;

                    -- Result set 2: quiz rank — classmates in the same classroom if this is a
                    -- classroom-linked (core) subject, otherwise other electors of it school-wide
                    IF @IsCore = 1
                        SELECT
                            qa.StudentId,
                            AVG(qa.FinalScorePercent) AS AvgScore,
                            COUNT(*) AS AttemptCount,
                            RANK() OVER (ORDER BY AVG(qa.FinalScorePercent) DESC) AS Position,
                            COUNT(*) OVER () AS TotalStudents
                        FROM QuizAttempt qa WITH (NOLOCK)
                        JOIN LessonContent lc WITH (NOLOCK) ON lc.Id = qa.LessonId
                        WHERE lc.SubjectId = @SubjectId
                          AND lc.ClassroomId = @ClassroomId
                          AND qa.SchoolId = @SchoolId
                          AND qa.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                          AND qa.FinalScorePercent IS NOT NULL
                        GROUP BY qa.StudentId
                        ORDER BY Position;
                    ELSE
                        SELECT
                            qa.StudentId,
                            AVG(qa.FinalScorePercent) AS AvgScore,
                            COUNT(*) AS AttemptCount,
                            RANK() OVER (ORDER BY AVG(qa.FinalScorePercent) DESC) AS Position,
                            COUNT(*) OVER () AS TotalStudents
                        FROM QuizAttempt qa WITH (NOLOCK)
                        JOIN LessonContent lc WITH (NOLOCK) ON lc.Id = qa.LessonId
                        WHERE lc.SubjectId = @SubjectId
                          AND qa.SchoolId = @SchoolId
                          AND qa.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                          AND qa.FinalScorePercent IS NOT NULL
                          AND qa.StudentId IN (
                              SELECT StudentId FROM StudentMinorSubject
                              WHERE SubjectId = @SubjectId AND IsActive = 1 AND SchoolId = @SchoolId
                          )
                        GROUP BY qa.StudentId
                        ORDER BY Position;

                    -- Resolve which assessments belong to this subject: direct Subject assignment,
                    -- a Classroom assignment where that classroom teaches the subject, or (for
                    -- individually-targeted assessments, ~34% of real assignments) via the subject
                    -- of the questions actually attached to the assessment.
                    DECLARE @RelevantAssessments TABLE (AssessmentId UNIQUEIDENTIFIER PRIMARY KEY);
                    INSERT INTO @RelevantAssessments
                    SELECT DISTINCT a.Id
                    FROM Assessments a WITH (NOLOCK)
                    JOIN AssessmentAssignment asg WITH (NOLOCK) ON asg.AssessmentId = a.Id AND asg.IsActive = 1
                    WHERE a.SchoolId = @SchoolId AND a.IsActive = 1
                    AND (
                        (asg.TargetType = 'Subject' AND asg.TargetId = @SubjectId)
                        OR (asg.TargetType = 'Classroom' AND asg.TargetId IN (
                            SELECT ClassroomId FROM ClassroomSubject
                            WHERE SubjectId = @SubjectId AND IsActive = 1 AND SchoolId = @SchoolId
                        ))
                        OR (asg.TargetType = 'Student' AND EXISTS (
                            SELECT 1 FROM AssessmentQuestion aq WITH (NOLOCK)
                            JOIN Questions q WITH (NOLOCK) ON q.Id = aq.QuestionId
                            WHERE aq.AssessmentId = a.Id AND aq.IsActive = 1 AND q.SubjectId = @SubjectId
                        ))
                    );

                    -- Result set 3: assessment rank — same classmates-vs-school-wide split as quiz
                    IF @IsCore = 1
                        SELECT
                            aa.StudentId,
                            AVG(aa.FinalScorePercent) AS AvgScore,
                            COUNT(*) AS AttemptCount,
                            RANK() OVER (ORDER BY AVG(aa.FinalScorePercent) DESC) AS Position,
                            COUNT(*) OVER () AS TotalStudents
                        FROM AssessmentAttempt aa WITH (NOLOCK)
                        JOIN StudentClassroom sc WITH (NOLOCK) ON sc.StudentId = aa.StudentId AND sc.ClassroomId = @ClassroomId AND sc.IsActive = 1
                        WHERE aa.SchoolId = @SchoolId
                          AND aa.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                          AND aa.FinalScorePercent IS NOT NULL
                          AND aa.AssessmentId IN (SELECT AssessmentId FROM @RelevantAssessments)
                        GROUP BY aa.StudentId
                        ORDER BY Position;
                    ELSE
                        SELECT
                            aa.StudentId,
                            AVG(aa.FinalScorePercent) AS AvgScore,
                            COUNT(*) AS AttemptCount,
                            RANK() OVER (ORDER BY AVG(aa.FinalScorePercent) DESC) AS Position,
                            COUNT(*) OVER () AS TotalStudents
                        FROM AssessmentAttempt aa WITH (NOLOCK)
                        WHERE aa.SchoolId = @SchoolId
                          AND aa.Status IN ('Submitted','PartiallyGraded','FullyGraded')
                          AND aa.FinalScorePercent IS NOT NULL
                          AND aa.AssessmentId IN (SELECT AssessmentId FROM @RelevantAssessments)
                          AND aa.StudentId IN (
                              SELECT StudentId FROM StudentMinorSubject
                              WHERE SubjectId = @SubjectId AND IsActive = 1 AND SchoolId = @SchoolId
                          )
                        GROUP BY aa.StudentId
                        ORDER BY Position;
                END",

                // Student study groups — core tables only (group + membership). Content
                // submission and approval routing are a separate, not-yet-built phase of
                // this feature; Status/approval columns exist now so no later migration
                // is needed once that phase lands.
                "IF OBJECT_ID('StudentGroup', 'U') IS NULL CREATE TABLE StudentGroup (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), SchoolId UNIQUEIDENTIFIER NOT NULL, ClassroomId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT 'PendingApproval', CreatedBy UNIQUEIDENTIFIER NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), ApprovedBy UNIQUEIDENTIFIER NULL, ApprovedAt DATETIME2 NULL, RejectedBy UNIQUEIDENTIFIER NULL, RejectionReason NVARCHAR(500) NULL, ModifiedAt DATETIME2 NULL, IsActive BIT NOT NULL DEFAULT 1, CONSTRAINT PK_StudentGroup PRIMARY KEY (Id))",
                "IF OBJECT_ID('StudentGroup', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentGroup_ClassroomId' AND object_id = OBJECT_ID('StudentGroup')) CREATE INDEX IX_StudentGroup_ClassroomId ON StudentGroup(ClassroomId) WHERE IsActive = 1",
                "IF OBJECT_ID('StudentGroup', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentGroup_CreatedBy' AND object_id = OBJECT_ID('StudentGroup')) CREATE INDEX IX_StudentGroup_CreatedBy ON StudentGroup(CreatedBy) WHERE IsActive = 1",
                "IF OBJECT_ID('StudentGroupMember', 'U') IS NULL CREATE TABLE StudentGroupMember (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), GroupId UNIQUEIDENTIFIER NOT NULL, StudentId UNIQUEIDENTIFIER NOT NULL, SchoolId UNIQUEIDENTIFIER NOT NULL, InvitedBy UNIQUEIDENTIFIER NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), IsActive BIT NOT NULL DEFAULT 1, CONSTRAINT PK_StudentGroupMember PRIMARY KEY (Id))",
                "IF OBJECT_ID('StudentGroupMember', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_StudentGroupMember_Group_Student_Active' AND object_id = OBJECT_ID('StudentGroupMember')) CREATE UNIQUE INDEX UQ_StudentGroupMember_Group_Student_Active ON StudentGroupMember(GroupId, StudentId) WHERE IsActive = 1",
                "IF OBJECT_ID('StudentGroupMember', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentGroupMember_GroupId' AND object_id = OBJECT_ID('StudentGroupMember')) CREATE INDEX IX_StudentGroupMember_GroupId ON StudentGroupMember(GroupId) WHERE IsActive = 1",
                "IF OBJECT_ID('StudentGroupMember', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentGroupMember_StudentId' AND object_id = OBJECT_ID('StudentGroupMember')) CREATE INDEX IX_StudentGroupMember_StudentId ON StudentGroupMember(StudentId) WHERE IsActive = 1",

                // Email-confirmed password change for non-Student roles — updatePassword stores
                // the intended new hash here instead of applying it immediately; the account's
                // registered email must confirm via token before it takes effect. Students are
                // exempt (same self-service email flow they're already excluded from elsewhere).
                "IF OBJECT_ID('PasswordChangeConfirmation', 'U') IS NULL CREATE TABLE PasswordChangeConfirmation (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), UserId UNIQUEIDENTIFIER NOT NULL, SchoolId UNIQUEIDENTIFIER NOT NULL, Token NVARCHAR(200) NOT NULL, PendingHashPassword NVARCHAR(200) NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), ExpiresAt DATETIME2 NOT NULL, IsUsed BIT NOT NULL DEFAULT 0, CONSTRAINT PK_PasswordChangeConfirmation PRIMARY KEY (Id))",
                "IF OBJECT_ID('PasswordChangeConfirmation', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PasswordChangeConfirmation_Token' AND object_id = OBJECT_ID('PasswordChangeConfirmation')) CREATE UNIQUE INDEX UQ_PasswordChangeConfirmation_Token ON PasswordChangeConfirmation(Token)",
                "IF OBJECT_ID('PasswordChangeConfirmation', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PasswordChangeConfirmation_UserId' AND object_id = OBJECT_ID('PasswordChangeConfirmation')) CREATE INDEX IX_PasswordChangeConfirmation_UserId ON PasswordChangeConfirmation(UserId)",

                // Student group content — the "drop content" phase of the study group feature.
                // Mirrors LessonContent/LessonMedia's shape; each submission goes through its
                // own ClassTeacher/HeadTeacher approval (OperationType.SubmitGroupContent)
                // before other group members (not the creator) can see it.
                "IF OBJECT_ID('GroupLessonContent', 'U') IS NULL CREATE TABLE GroupLessonContent (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), SchoolId UNIQUEIDENTIFIER NOT NULL, GroupId UNIQUEIDENTIFIER NOT NULL, SubjectId UNIQUEIDENTIFIER NOT NULL, TopicId UNIQUEIDENTIFIER NULL, SubTopic NVARCHAR(200) NULL, Aim NVARCHAR(500) NOT NULL, Description NVARCHAR(2000) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT 'PendingApproval', CreatedBy UNIQUEIDENTIFIER NOT NULL, ApprovedBy UNIQUEIDENTIFIER NULL, ApprovedAt DATETIME2 NULL, RejectedBy UNIQUEIDENTIFIER NULL, RejectionReason NVARCHAR(500) NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), ModifiedAt DATETIME2 NULL, CONSTRAINT PK_GroupLessonContent PRIMARY KEY (Id))",
                "IF OBJECT_ID('GroupLessonContent', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GroupLessonContent_GroupId' AND object_id = OBJECT_ID('GroupLessonContent')) CREATE INDEX IX_GroupLessonContent_GroupId ON GroupLessonContent(GroupId)",
                "IF OBJECT_ID('GroupLessonContent', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GroupLessonContent_CreatedBy' AND object_id = OBJECT_ID('GroupLessonContent')) CREATE INDEX IX_GroupLessonContent_CreatedBy ON GroupLessonContent(CreatedBy)",

                // A student writing an essay-style text response directly, with no
                // board recording and no attached media — a third valid content
                // shape alongside media/board, not a replacement for Description
                // (which stays a short blurb, capped at 2000 chars).
                "IF OBJECT_ID('GroupLessonContent', 'U') IS NOT NULL AND COL_LENGTH('GroupLessonContent', 'TextContent') IS NULL ALTER TABLE GroupLessonContent ADD TextContent NVARCHAR(MAX) NULL",
                "IF OBJECT_ID('GroupLessonMedia', 'U') IS NULL CREATE TABLE GroupLessonMedia (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), GroupContentId UNIQUEIDENTIFIER NOT NULL, SchoolId UNIQUEIDENTIFIER NOT NULL, FileName NVARCHAR(300) NOT NULL, OriginalFileName NVARCHAR(300) NOT NULL, FileExtension NVARCHAR(20) NOT NULL, MediaType NVARCHAR(50) NOT NULL, FileSizeBytes BIGINT NOT NULL DEFAULT 0, CloudinaryUrl NVARCHAR(1000) NOT NULL, PublicId NVARCHAR(500) NOT NULL, Duration INT NULL, Status NVARCHAR(50) NOT NULL DEFAULT 'Ready', DisplayOrder INT NOT NULL DEFAULT 1, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), IsActive BIT NOT NULL DEFAULT 1, MetaData NVARCHAR(MAX) NULL, CONSTRAINT PK_GroupLessonMedia PRIMARY KEY (Id))",
                "IF OBJECT_ID('GroupLessonMedia', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GroupLessonMedia_GroupContentId' AND object_id = OBJECT_ID('GroupLessonMedia')) CREATE INDEX IX_GroupLessonMedia_GroupContentId ON GroupLessonMedia(GroupContentId) WHERE IsActive = 1",

                // Questions.Title was NVARCHAR(500) — too small for long-form theory/
                // practical exam questions, where the "title" the frontend sends is the
                // full question text (the same content also goes into TextContent, which
                // is NVARCHAR(MAX)). Caused "String or binary data would be truncated" on
                // /api/questions/batch for any question over 500 characters.
                @"IF OBJECT_ID('Questions', 'U') IS NOT NULL AND EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Questions' AND COLUMN_NAME = 'Title' AND CHARACTER_MAXIMUM_LENGTH <> -1)
                  ALTER TABLE Questions ALTER COLUMN Title NVARCHAR(MAX) NOT NULL",

                // Staff-initiated password reset for a locked-out/forgotten-password
                // student (students have no self-service forgot-password path by
                // design). RequirePasswordChange forces the SAME "set your own
                // password" completion flow already used for first-time login
                // (update-password/newUser) — the temp password only ever works to
                // get to that screen, never as an ongoing password.
                "IF OBJECT_ID('Users', 'U') IS NOT NULL AND COL_LENGTH('Users', 'RequirePasswordChange') IS NULL ALTER TABLE Users ADD RequirePasswordChange BIT NOT NULL DEFAULT 0",

                // Temp password is only valid for 1 hour after a staff-initiated
                // reset — past that, login rejects it outright rather than letting
                // an unused reset sit as a standing valid credential forever.
                "IF OBJECT_ID('Users', 'U') IS NOT NULL AND COL_LENGTH('Users', 'PasswordResetExpiresAt') IS NULL ALTER TABLE Users ADD PasswordResetExpiresAt DATETIME2 NULL",

                // Audit trail: which staff member reset which student's password, and
                // when — so a school can tell a legitimate reset from someone
                // impersonating a student to get another student's account reset.
                "IF OBJECT_ID('StudentPasswordResetLog', 'U') IS NULL CREATE TABLE StudentPasswordResetLog (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), StudentId UNIQUEIDENTIFIER NOT NULL, SchoolId UNIQUEIDENTIFIER NOT NULL, ResetBy UNIQUEIDENTIFIER NOT NULL, ResetByName NVARCHAR(200) NOT NULL, ResetByRole NVARCHAR(50) NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), CONSTRAINT PK_StudentPasswordResetLog PRIMARY KEY (Id))",
                "IF OBJECT_ID('StudentPasswordResetLog', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentPasswordResetLog_StudentId' AND object_id = OBJECT_ID('StudentPasswordResetLog')) CREATE INDEX IX_StudentPasswordResetLog_StudentId ON StudentPasswordResetLog(StudentId)",

                // In-app notifications (fan-out-on-write: one row per recipient).
                // IsDelivered = shown once as a popup (e.g. right after login);
                // IsRead = explicitly opened/dismissed. A notification a student
                // never noticed stays in their list until read — it doesn't
                // vanish just because they logged in again.
                "IF OBJECT_ID('Notification', 'U') IS NULL CREATE TABLE Notification (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), SchoolId UNIQUEIDENTIFIER NOT NULL, RecipientId UNIQUEIDENTIFIER NOT NULL, Type NVARCHAR(50) NOT NULL, Title NVARCHAR(200) NOT NULL, Body NVARCHAR(1000) NOT NULL, EntityType NVARCHAR(50) NULL, EntityId UNIQUEIDENTIFIER NULL, IsDelivered BIT NOT NULL DEFAULT 0, IsRead BIT NOT NULL DEFAULT 0, CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(), ReadAt DATETIME2 NULL, CONSTRAINT PK_Notification PRIMARY KEY (Id))",
                "IF OBJECT_ID('Notification', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notification_Recipient' AND object_id = OBJECT_ID('Notification')) CREATE INDEX IX_Notification_Recipient ON Notification(RecipientId, CreatedAt DESC)",

                // Run history for the periodic 24h IHostedService workers (performance
                // aggregation, admin dashboard aggregation) — NOT Hangfire, which
                // already tracks its own jobs in its own SQL schema. Answers "did
                // today's aggregation actually run, when, how long did it take, did
                // it fail" without having to grep raw log files for it.
                "IF OBJECT_ID('BackgroundJobRun', 'U') IS NULL CREATE TABLE BackgroundJobRun (Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), JobName NVARCHAR(100) NOT NULL, StartedAt DATETIME2 NOT NULL, CompletedAt DATETIME2 NULL, DurationMs BIGINT NULL, Status NVARCHAR(20) NOT NULL DEFAULT 'Running', ErrorMessage NVARCHAR(MAX) NULL, Details NVARCHAR(MAX) NULL, CONSTRAINT PK_BackgroundJobRun PRIMARY KEY (Id))",
                "IF OBJECT_ID('BackgroundJobRun', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BackgroundJobRun_JobName' AND object_id = OBJECT_ID('BackgroundJobRun')) CREATE INDEX IX_BackgroundJobRun_JobName ON BackgroundJobRun(JobName, StartedAt DESC)",

                // LessonContent.Aim was nvarchar(500) — SubmitLessonViewModel's
                // matching [StringLength(500)] has been removed so teachers can write
                // longer lesson aims/objectives, but the column itself must be widened
                // too or a longer submission just fails at the DB with a truncation
                // error instead of the old 400. max_length = -1 means already MAX.
                @"IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
                             WHERE c.object_id = OBJECT_ID('LessonContent') AND c.name = 'Aim' AND c.max_length <> -1)
                  ALTER TABLE LessonContent ALTER COLUMN Aim NVARCHAR(MAX) NOT NULL",

                // Admin-only question bank tier: a question tagged IsAdminOnly=1 is
                // extracted/created by an Administrator/SuperAdministrator and is never
                // visible to teachers (in browse/listing, single-fetch, or when picking
                // QuestionIds for a quiz/assessment) — lets admins independently verify
                // whether students truly understand a topic, using questions the
                // teacher's own (exhaustible) pool never exposed. Defaults to 0 so every
                // existing row and every ordinary teacher-created row is unaffected.
                "IF OBJECT_ID('Questions', 'U') IS NOT NULL AND COL_LENGTH('Questions', 'IsAdminOnly') IS NULL ALTER TABLE Questions ADD IsAdminOnly BIT NOT NULL DEFAULT 0",

                // Captured once at job-submit time (from the submitter's role) so the
                // background AI-extraction worker knows whether to tag the Questions
                // rows it inserts for this job as admin-only.
                "IF OBJECT_ID('QuestionJob', 'U') IS NOT NULL AND COL_LENGTH('QuestionJob', 'IsAdminOnly') IS NULL ALTER TABLE QuestionJob ADD IsAdminOnly BIT NOT NULL DEFAULT 0"
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

            // One-time backfill: scanned questions were saved with Title = '' (only
            // QuestionHtml carried real content — see QuestionJobService), so the
            // question list screen had nothing to show and displayed "Untitled".
            // Self-limiting: once every row has a real title, the SELECT finds
            // nothing and this is a no-op on every subsequent startup.
            try
            {
                var toBackfill = new List<(Guid Id, string Html)>();

                await using (var selectCmd = new SqlCommand(
                    @"SELECT Id, QuestionHtml FROM Questions
                      WHERE (Title IS NULL OR Title = '' OR Title LIKE '{{image:%')
                      AND QuestionHtml IS NOT NULL AND QuestionHtml <> ''",
                    connection))
                await using (var titleReader = await selectCmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await titleReader.ReadAsync(cancellationToken))
                    {
                        toBackfill.Add((titleReader.GetGuid(0), titleReader.GetString(1)));
                    }
                }

                foreach (var (id, html) in toBackfill)
                {
                    var derivedTitle = DeriveTitleFromHtml(html);
                    if (string.IsNullOrEmpty(derivedTitle)) continue;

                    await using var updateCmd = new SqlCommand(
                        "UPDATE Questions SET Title = @Title WHERE Id = @Id", connection);
                    updateCmd.Parameters.AddWithValue("@Title", derivedTitle);
                    updateCmd.Parameters.AddWithValue("@Id", id);
                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                if (toBackfill.Count > 0)
                {
                    Log.Information(
                        "DatabaseInitializer: Backfilled Title for {Count} scanned question(s) from QuestionHtml",
                        toBackfill.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DatabaseInitializer: Question title backfill failed (non-fatal).");
            }

            // One-time backfill: every AttendanceSession ever created was left
            // Status=0 (Open) forever because no teacher ever called the explicit
            // "end session" endpoint (scanning now auto-closes sessions going
            // forward, but this recovers historical sessions that already have
            // real scans). Self-limiting: once no Open session has a scan, this
            // UPDATE affects 0 rows on every subsequent startup.
            try
            {
                await using var backfillCmd = new SqlCommand(
                    @"UPDATE s SET Status = 1, EndedAt = latestRecord.LastAttendedAt, ModifiedDate = CONVERT(VARCHAR, GETUTCDATE(), 120)
                      FROM AttendanceSession s
                      CROSS APPLY (
                          SELECT MAX(AttendedAt) AS LastAttendedAt
                          FROM AttendanceRecord
                          WHERE SessionId = s.Id AND IsActive = 1
                      ) latestRecord
                      WHERE s.Status = 0 AND latestRecord.LastAttendedAt IS NOT NULL",
                    connection);
                backfillCmd.CommandTimeout = 120;
                var rowsClosed = await backfillCmd.ExecuteNonQueryAsync(cancellationToken);

                if (rowsClosed > 0)
                {
                    Log.Information(
                        "DatabaseInitializer: Backfilled Status=Closed for {Count} stuck-open AttendanceSession row(s) with real scans",
                        rowsClosed);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DatabaseInitializer: AttendanceSession backfill failed (non-fatal).");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DatabaseInitializer: Schema migration block failed (non-fatal).");
        }
    }

    // Mirrors QuestionJobService.DeriveTitleFromHtml — kept in sync manually since
    // this project has no dependency on TeachHub.QuestionBank.
    private static string DeriveTitleFromHtml(string? html)
    {
        const int maxLength = 150;

        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\{\{\s*image\s*:[^}]*\}\}", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (text.Length == 0)
            return "Image-based question";

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength).TrimEnd() + "…";
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
