CREATE TABLE TenantInfo (
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),        -- Guid, not int!
    SchoolId UNIQUEIDENTIFIER NOT NULL,                   -- Guid, not int!
    Identifier NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    ConnectionString NVARCHAR(500) NULL,
    CreatedDate DATETIME2 NOT NULL DEFAULT GETDATE(),     -- CreatedDate, not CreatedAt!
    ModifiedDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    SettingsJson NVARCHAR(MAX) NULL,                      -- JSON string, not Dictionary!
    
    CONSTRAINT PK_TenantInfo PRIMARY KEY (Id),
    CONSTRAINT FK_TenantInfo_School FOREIGN KEY (SchoolId) 
        REFERENCES School(Id),
    CONSTRAINT UQ_TenantInfo_Identifier UNIQUE (Identifier),
    CONSTRAINT UQ_TenantInfo_SchoolId UNIQUE (SchoolId)
);

------------------------------------------------------------------------------------------------------------------------------------

CREATE TABLE Subjects
(
    Id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_Subjects PRIMARY KEY
        DEFAULT NEWID(),

    CreationDate DATETIME2 NOT NULL
        CONSTRAINT DF_Subjects_CreationDate DEFAULT SYSUTCDATETIME(),

    ModifiedDate DATETIME2 NOT NULL
        CONSTRAINT DF_Subjects_ModifiedDate DEFAULT SYSUTCDATETIME(),

    Subject NVARCHAR(255) NOT NULL,

    Category INT NOT NULL,         -- SubjectCategory enum
    ClassCategory INT NOT NULL,    -- ClassCategory enum

    SchoolId UNIQUEIDENTIFIER NOT NULL,

    CreatedBy UNIQUEIDENTIFIER NOT NULL,

    IsActive BIT NOT NULL
        CONSTRAINT DF_Subjects_IsActive DEFAULT (1)
);

--------------------------------------------------------------------------------------

 CREATE TABLE ClassroomSubject
(
    Id UNIQUEIDENTIFIER NOT NULL 
        CONSTRAINT PK_ClassroomSubjects PRIMARY KEY,

    CreationDate DATETIME2(7) NOT NULL 
        CONSTRAINT DF_ClassroomSubjects_CreationDate 
        DEFAULT SYSDATETIME(),

    ModifiedDate DATETIME2(7) NOT NULL 
        CONSTRAINT DF_ClassroomSubjects_ModifiedDate 
        DEFAULT SYSDATETIME(),

    ClassroomId UNIQUEIDENTIFIER NOT NULL,
    SubjectId UNIQUEIDENTIFIER NOT NULL,
    SchoolId UNIQUEIDENTIFIER NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,

    IsActive BIT NOT NULL 
        CONSTRAINT DF_ClassroomSubjects_IsActive DEFAULT 1,

    -- Foreign Keys
    CONSTRAINT FK_ClassroomSubjects_Classroom 
        FOREIGN KEY (ClassroomId) 
        REFERENCES Classroom(Id),

    CONSTRAINT FK_ClassroomSubjects_Subject 
        FOREIGN KEY (SubjectId) 
        REFERENCES Subjects(Id),

    CONSTRAINT FK_ClassroomSubjects_School 
        FOREIGN KEY (SchoolId) 
        REFERENCES School(Id)
);

------------------------------------------------------
 CREATE TABLE ClassroomTeacher (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ClassroomId UNIQUEIDENTIFIER NOT NULL,
    TeacherId UNIQUEIDENTIFIER NOT NULL,
    IsPrimary BIT DEFAULT 0,  -- Designate primary/head teacher for classroom
    CreationDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    ModifiedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    SchoolId UNIQUEIDENTIFIER NOT NULL,
    IsActive BIT DEFAULT 1,
    
    -- Foreign keys
    CONSTRAINT FK_ClassroomTeacher_Classroom FOREIGN KEY (ClassroomId) REFERENCES Classroom(Id),
    CONSTRAINT FK_ClassroomTeacher_Teacher FOREIGN KEY (TeacherId) REFERENCES Users(Id),
    CONSTRAINT FK_ClassroomTeacher_School FOREIGN KEY (SchoolId) REFERENCES School(Id),
    CONSTRAINT FK_ClassroomTeacher_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
    
    -- Unique constraint (one teacher can't be assigned to same classroom twice)
    CONSTRAINT UQ_ClassroomTeacher_Classroom_Teacher UNIQUE (ClassroomId, TeacherId)
);
--------------------------------------------------------------------------

CREATE TABLE AdminPermissions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    SchoolId UNIQUEIDENTIFIER NOT NULL,
    
    -- ✅ Single column for all permissions (bitwise flags)
    Permissions INT NOT NULL DEFAULT 0,  -- Stores enum flags as integer
    
    CreationDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    ModifiedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    IsActive BIT DEFAULT 1,
    
    CONSTRAINT FK_AdminPermissions_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_AdminPermissions_School FOREIGN KEY (SchoolId) REFERENCES School(Id),
    CONSTRAINT FK_AdminPermissions_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
    CONSTRAINT UQ_AdminPermissions_User_School UNIQUE (UserId, SchoolId)
);

CREATE INDEX IX_AdminPermissions_UserId ON AdminPermissions(UserId);
CREATE INDEX IX_AdminPermissions_SchoolId ON AdminPermissions(SchoolId);
CREATE INDEX IX_AdminPermissions_Permissions ON AdminPermissions(Permissions);

------------------------------------------------------------------------------------

-- ========================================
-- DROP EXISTING TABLES (if re-creating)
-- ========================================
IF OBJECT_ID('ClassPreparationMedia', 'U') IS NOT NULL 
    DROP TABLE ClassPreparationMedia;

IF OBJECT_ID('ClassPreparation', 'U') IS NOT NULL 
    DROP TABLE ClassPreparation;

-- ========================================
-- CLASS PREPARATION TABLE
-- ========================================
CREATE TABLE ClassPreparation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ClassroomId UNIQUEIDENTIFIER NOT NULL,
    SubjectId UNIQUEIDENTIFIER NOT NULL,
    TeacherId UNIQUEIDENTIFIER NOT NULL,
    SchoolId UNIQUEIDENTIFIER NOT NULL,
    
    -- Class details
    Title NVARCHAR(200) NOT NULL,
    Topic NVARCHAR(200) NOT NULL,
    SubTopic NVARCHAR(200),
    AimAndObjectives NVARCHAR(MAX) NOT NULL,
    
    -- Timing
    ScheduledDate DATE,
    ScheduledTime TIME,
    DurationMinutes INT,
    
    -- Class type
    ClassType INT NOT NULL,  -- 1=LiveClass, 2=RecordedClass, 3=InteractiveClass
    
    -- Status & Approval workflow
    Status INT NOT NULL DEFAULT 0,  -- 0=Draft, 1=Pending, 2=Approved, 3=Rejected, 4=InProgress, 5=Completed
    
    -- Submission tracking
    SubmittedForApprovalDate DATETIME,
    SubmittedBy UNIQUEIDENTIFIER,
    
    -- Approval tracking
    ApprovedBy UNIQUEIDENTIFIER,
    ApprovedDate DATETIME,
    
    -- Rejection tracking
    RejectedBy UNIQUEIDENTIFIER,
    RejectedDate DATETIME,
    RejectionReason NVARCHAR(500),
    
    -- Metadata
    CreationDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    ModifiedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    IsActive BIT DEFAULT 1,
    
    -- Foreign keys
    CONSTRAINT FK_ClassPreparation_Classroom 
        FOREIGN KEY (ClassroomId) REFERENCES StudentClass(Id),
    CONSTRAINT FK_ClassPreparation_Subject 
        FOREIGN KEY (SubjectId) REFERENCES Subject(Id),
    CONSTRAINT FK_ClassPreparation_Teacher 
        FOREIGN KEY (TeacherId) REFERENCES Users(Id),
    CONSTRAINT FK_ClassPreparation_School 
        FOREIGN KEY (SchoolId) REFERENCES School(Id)
);

-- Indexes for performance
CREATE INDEX IX_ClassPreparation_Status 
    ON ClassPreparation(Status);

CREATE INDEX IX_ClassPreparation_SchoolId_Status 
    ON ClassPreparation(SchoolId, Status);

CREATE INDEX IX_ClassPreparation_TeacherId 
    ON ClassPreparation(TeacherId);

CREATE INDEX IX_ClassPreparation_ClassroomId 
    ON ClassPreparation(ClassroomId);

CREATE INDEX IX_ClassPreparation_ScheduledDate 
    ON ClassPreparation(ScheduledDate) WHERE ScheduledDate IS NOT NULL;

-- ========================================
-- CLASS PREPARATION MEDIA TABLE
-- ========================================
CREATE TABLE ClassPreparationMedia (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ClassPreparationId UNIQUEIDENTIFIER,  -- NULL until linked to a class
    
    -- Unique identifiers
    MediaKey NVARCHAR(100) NOT NULL UNIQUE,  -- Permanent, unique filename
    PublicId NVARCHAR(200),  -- Cloudinary public_id
    
    -- Media details
    MediaType INT NOT NULL,  -- 1=Audio, 2=Video, 3=Document, 4=Image
    OriginalFileName NVARCHAR(200) NOT NULL,
    DisplayName NVARCHAR(200),
    
    -- File info
    FileSizeBytes BIGINT NOT NULL,
    OriginalSizeBytes BIGINT,  -- Size before compression
    DurationSeconds INT,
    MimeType NVARCHAR(100),
    FileExtension NVARCHAR(10),
    
    -- Checksums for duplicate detection
    SHA256Hash NVARCHAR(64),  -- File content hash
    
    -- Cloudinary storage
    CdnUrl NVARCHAR(500) NOT NULL,
    ThumbnailUrl NVARCHAR(500),  -- For video thumbnails
    CdnProvider NVARCHAR(50) DEFAULT 'Cloudinary',
    
    -- Storage location tracking
    IsTemporary BIT DEFAULT 1,  -- TRUE = temp/pending, FALSE = schools/permanent
    
    -- Download tracking
    DownloadCount INT DEFAULT 0,
    LastDownloadDate DATETIME,
    LastDownloadedBy UNIQUEIDENTIFIER,
    
    -- Deletion tracking (soft delete)
    IsDeleted BIT DEFAULT 0,
    DeletedDate DATETIME,
    DeletedBy UNIQUEIDENTIFIER,
    DeletionReason NVARCHAR(500),
    
    -- Metadata
    UploadedDate DATETIME DEFAULT GETUTCDATE(),
    SchoolId UNIQUEIDENTIFIER NOT NULL,
    CreationDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    IsActive BIT DEFAULT 1,
    
    -- Foreign keys
    CONSTRAINT FK_ClassPreparationMedia_ClassPreparation 
        FOREIGN KEY (ClassPreparationId) 
        REFERENCES ClassPreparation(Id) 
        ON DELETE SET NULL,  -- Don't cascade delete, keep orphaned media for tracking
    
    CONSTRAINT FK_ClassPreparationMedia_School 
        FOREIGN KEY (SchoolId) 
        REFERENCES School(Id),
    
    CONSTRAINT FK_ClassPreparationMedia_CreatedBy 
        FOREIGN KEY (CreatedBy) 
        REFERENCES Users(Id),
    
    CONSTRAINT FK_ClassPreparationMedia_DeletedBy 
        FOREIGN KEY (DeletedBy) 
        REFERENCES Users(Id)
);

-- Indexes for performance
CREATE INDEX IX_ClassPreparationMedia_ClassPreparationId 
    ON ClassPreparationMedia(ClassPreparationId);

CREATE INDEX IX_ClassPreparationMedia_MediaKey 
    ON ClassPreparationMedia(MediaKey);

CREATE INDEX IX_ClassPreparationMedia_SHA256Hash 
    ON ClassPreparationMedia(SHA256Hash);

CREATE INDEX IX_ClassPreparationMedia_IsTemporary 
    ON ClassPreparationMedia(IsTemporary) 
    WHERE IsTemporary = 1;

CREATE INDEX IX_ClassPreparationMedia_IsDeleted 
    ON ClassPreparationMedia(IsDeleted) 
    WHERE IsDeleted = 1;

CREATE INDEX IX_ClassPreparationMedia_SchoolId 
    ON ClassPreparationMedia(SchoolId);

CREATE INDEX IX_ClassPreparationMedia_PublicId 
    ON ClassPreparationMedia(PublicId) 
    WHERE PublicId IS NOT NULL;






    -- Add UploadStatus and UploadErrorMessage columns

-- Add UploadStatus column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[ClassPreparationMedia]') 
               AND name = 'UploadStatus')
BEGIN
    ALTER TABLE ClassPreparationMedia
    ADD UploadStatus INT NOT NULL DEFAULT 0;
    
    PRINT 'UploadStatus column added';
END

-- Add UploadErrorMessage column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[ClassPreparationMedia]') 
               AND name = 'UploadErrorMessage')
BEGIN
    ALTER TABLE ClassPreparationMedia
    ADD UploadErrorMessage NVARCHAR(500) NULL;
    
    PRINT 'UploadErrorMessage column added';
END

-- Add ModifiedDate column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[ClassPreparationMedia]') 
               AND name = 'ModifiedDate')
BEGIN
    ALTER TABLE ClassPreparationMedia
    ADD ModifiedDate DATETIME NULL;
    
    PRINT 'ModifiedDate column added';
END

-- Update existing records to have default values
UPDATE ClassPreparationMedia
SET UploadStatus = 2,  -- Set existing records to "Completed"
    ModifiedDate = CreationDate
WHERE UploadStatus IS NULL OR UploadStatus = 0;

PRINT 'Migration completed successfully';

-- Create index for upload status queries
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_ClassPreparationMedia_UploadStatus' 
               AND object_id = OBJECT_ID('ClassPreparationMedia'))
BEGIN
    CREATE INDEX IX_ClassPreparationMedia_UploadStatus 
    ON ClassPreparationMedia(UploadStatus, CreationDate);
    
    PRINT 'Index created on UploadStatus';
END
-- ========================================
-- SAMPLE DATA (Optional - for testing)
-- ========================================

-- Note: Insert sample data after creating required dependencies:
-- School, Users (Teacher), StudentClass (Classroom), Subject

/*
-- Example: Insert a draft class preparation
INSERT INTO ClassPreparation (
    Id, ClassroomId, SubjectId, TeacherId, SchoolId,
    Title, Topic, AimAndObjectives, ClassType, Status,
    CreatedBy, CreationDate, ModifiedDate, IsActive
)
VALUES (
    NEWID(),
    'classroom-guid-here',
    'subject-guid-here',
    'teacher-guid-here',
    'school-guid-here',
    'Physics',
    'Molecule And Matter',
    'To introduce students to the concept of molecules...',
    1,  -- LiveClass
    0,  -- Draft
    'teacher-guid-here',
    GETUTCDATE(),
    GETUTCDATE(),
    1
);
*/
-------------------------------------------------------------------------------------------------

-- Create separate table for teacher performance tracking
-- This keeps User table clean and allows historical tracking

CREATE TABLE TeacherTrustScore
(
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    
    -- Foreign Keys
    TeacherId UNIQUEIDENTIFIER NOT NULL,
    SchoolId UNIQUEIDENTIFIER NOT NULL,
    
    -- Performance Metrics
    TrustScore DECIMAL(5,2) NOT NULL DEFAULT 0.00,  -- 0.00 to 100.00
    TotalClassesSubmitted INT NOT NULL DEFAULT 0,
    TotalClassesApproved INT NOT NULL DEFAULT 0,
    TotalClassesRejected INT NOT NULL DEFAULT 0,
    
    -- Timing Metrics
    AverageApprovalTimeMinutes INT NOT NULL DEFAULT 0,
    FastestApprovalMinutes INT NULL,
    SlowestApprovalMinutes INT NULL,
    
    -- Quality Indicators
    ConsecutiveApprovals INT NOT NULL DEFAULT 0,  -- Current streak
    BestApprovalStreak INT NOT NULL DEFAULT 0,    -- Historical best
    LastRejectionDate DATETIME NULL,
    LastApprovalDate DATETIME NULL,
    
    -- Audit Fields
    LastCalculatedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CreationDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    ModifiedDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
    
    -- Constraints
    CONSTRAINT FK_TeacherTrustScore_Teacher FOREIGN KEY (TeacherId) REFERENCES Users(Id),
    CONSTRAINT FK_TeacherTrustScore_School FOREIGN KEY (SchoolId) REFERENCES School(Id),
    CONSTRAINT UQ_TeacherTrustScore_Teacher_School UNIQUE (TeacherId, SchoolId)
);

-- Indexes for performance
CREATE INDEX IX_TeacherTrustScore_TeacherId ON TeacherTrustScore(TeacherId);
CREATE INDEX IX_TeacherTrustScore_TrustScore ON TeacherTrustScore(TrustScore DESC);
CREATE INDEX IX_TeacherTrustScore_School ON TeacherTrustScore(SchoolId);

-- Comments explaining table purpose
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Tracks teacher performance metrics for intelligent approval workflows. Calculated based on approval history, timing, and quality indicators.', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'TeacherTrustScore';
------------------------------------------------------------------------------------------------------

-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
-- Table: ClassPreparationMedia
-- Changes: Add columns for admin preview and AI analysis
-- 
-- NEW COLUMNS:
-- - ThumbnailUrl: Auto-generated video thumbnail (for quick preview)
-- - PreviewUrl: First 30 seconds of video (for admin review)
-- - AIAnalysisStatus: Track AI content analysis progress
-- - AIAnalysisData: JSON with AI results (content flags, quality metrics)
-- - KeyMoments: JSON array of important timestamps in video
-- - TranscriptText: Full transcript (future feature)
-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

-- Thumbnail URL (auto-generated by Cloudinary)
-- Example: https://res.cloudinary.com/.../video.jpg
-- Used in admin quick preview
ALTER TABLE ClassPreparationMedia
ADD ThumbnailUrl NVARCHAR(500) NULL;

-- Preview URL (first 30 seconds of video)
-- Example: https://res.cloudinary.com/.../video_preview.mp4
-- Allows admin to quickly review without downloading full file
ALTER TABLE ClassPreparationMedia
ADD PreviewUrl NVARCHAR(500) NULL;

-- AI Analysis Status
-- 0 = Pending (not started)
-- 1 = Processing (AI job running)
-- 2 = Completed (results available)
-- 3 = Failed (error occurred)
ALTER TABLE ClassPreparationMedia
ADD AIAnalysisStatus INT DEFAULT 0 NOT NULL;

-- AI Analysis Results (JSON format)
-- Stores: content flags, quality metrics, detected topics
-- Example: {"inappropriate": false, "educational": true, "quality": "good"}
ALTER TABLE ClassPreparationMedia
ADD AIAnalysisData NVARCHAR(MAX) NULL;

-- Key Moments (JSON array of timestamps)
-- Auto-detected or teacher-marked important points in video
-- Example: [{"time": 0, "label": "Introduction"}, {"time": 135, "label": "Main Content"}]
-- Used for admin to skip to important sections
ALTER TABLE ClassPreparationMedia
ADD KeyMoments NVARCHAR(MAX) NULL;

-- Transcript Text (full video transcript)
-- Future feature: Speech-to-text transcription
-- Used for: search, accessibility, content analysis
ALTER TABLE ClassPreparationMedia
ADD TranscriptText NVARCHAR(MAX) NULL;

-- Index for AI Analysis Queries
-- Common query: Find all media needing AI analysis
-- Or: Find all failed AI analyses for retry
CREATE INDEX IX_ClassPreparationMedia_AIAnalysisStatus 
ON ClassPreparationMedia(AIAnalysisStatus, CreationDate);

-- Column Descriptions
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Auto-generated thumbnail image URL for video preview (Cloudinary)', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparationMedia',
    @level2type = N'COLUMN', @level2name = 'ThumbnailUrl';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'URL to first 30 seconds of video for admin quick preview', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparationMedia',
    @level2type = N'COLUMN', @level2name = 'PreviewUrl';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'AI content analysis status: 0=Pending, 1=Processing, 2=Completed, 3=Failed', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparationMedia',
    @level2type = N'COLUMN', @level2name = 'AIAnalysisStatus';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'JSON containing AI analysis results (content flags, quality metrics, topics)', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparationMedia',
    @level2type = N'COLUMN', @level2name = 'AIAnalysisData';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'JSON array of key timestamps in video with labels for admin navigation', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparationMedia',
    @level2type = N'COLUMN', @level2name = 'KeyMoments';

-----------------------------------------------------------------------------

-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
-- Table: ClassPreparation
-- Changes: Add columns for approval tracking and admin workflow
-- 
-- NEW COLUMNS:
-- - ApprovalNotes: Admin's feedback/comments when approving
-- - ApprovalTimeMinutes: How long admin took to review (performance metric)
-- - IsUrgent: Flag for classes starting < 24 hours (priority queue)
-- - NeedsReview: Flag for manual review (new teacher, large files, etc.)
-- - AutoApprovalEligible: Can be bulk-approved (trusted teacher + AI validated)
-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

-- Admin's approval notes/comments
-- Optional feedback from admin to teacher
-- Example: "Great presentation, approved for use"
ALTER TABLE ClassPreparation
ADD ApprovalNotes NVARCHAR(1000) NULL;

-- Time admin took to review and approve (in minutes)
-- Calculated from: SubmittedForApprovalDate to ApprovedDate
-- Used for: Admin performance metrics, trust score calculation
-- Example: 5 (admin took 5 minutes to review)
ALTER TABLE ClassPreparation
ADD ApprovalTimeMinutes INT NULL;

-- Urgent flag (class starts in < 24 hours)
-- Auto-calculated or manually set
-- Used for: Priority sorting in admin dashboard
-- Urgent classes shown at top of approval queue
ALTER TABLE ClassPreparation
ADD IsUrgent BIT DEFAULT 0 NOT NULL;

-- Needs manual review flag
-- Triggered by: New teacher, large files, AI flags, etc.
-- Used for: Separating routine approvals from those needing attention
-- Admins can bulk-approve non-flagged classes
ALTER TABLE ClassPreparation
ADD NeedsReview BIT DEFAULT 0 NOT NULL;

-- Auto-approval eligible flag
-- Criteria: Trusted teacher + All media AI-validated + Normal file sizes
-- Used for: Bulk approval workflows
-- Admins can approve 20+ classes with one click if all eligible
ALTER TABLE ClassPreparation
ADD AutoApprovalEligible BIT DEFAULT 0 NOT NULL;

-- Index for Admin Dashboard Queries
-- Most common query: Get pending approvals sorted by priority
-- Covers: Status, IsUrgent, SubmittedForApprovalDate
CREATE INDEX IX_ClassPreparation_ApprovalQueue 
ON ClassPreparation(Status, IsUrgent DESC, SubmittedForApprovalDate DESC)
WHERE Status = 1;  -- Filtered index (only Pending status)

-- Index for Bulk Approval Queries
-- Find all auto-approval eligible classes
CREATE INDEX IX_ClassPreparation_AutoApprovalEligible 
ON ClassPreparation(AutoApprovalEligible, Status)
WHERE AutoApprovalEligible = 1 AND Status = 1;

-- Column Descriptions
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Admin feedback/comments when approving class (optional)', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparation',
    @level2type = N'COLUMN', @level2name = 'ApprovalNotes';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Time in minutes admin took to review and approve class (performance metric)', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparation',
    @level2type = N'COLUMN', @level2name = 'ApprovalTimeMinutes';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Flag indicating class starts in < 24 hours and needs urgent approval', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparation',
    @level2type = N'COLUMN', @level2name = 'IsUrgent';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Flag indicating class needs manual review (new teacher, AI flags, large files)', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparation',
    @level2type = N'COLUMN', @level2name = 'NeedsReview';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Flag indicating class can be bulk-approved (trusted teacher + AI validated)', 
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE',  @level1name = 'ClassPreparation',
    @level2type = N'COLUMN', @level2name = 'AutoApprovalEligible';

-----------------------------------------------------------------------------------------
CREATE TABLE EmailTemplates (
    Id          UNIQUEIDENTIFIER PRIMARY KEY
                DEFAULT NEWID(),
    TemplateKey NVARCHAR(100) NOT NULL,
    -- e.g. "welcome_user", "password_reset"
    Subject     NVARCHAR(200) NOT NULL,
    HtmlBody    NVARCHAR(MAX) NOT NULL,
    -- Contains placeholders like {{UserName}}
    -- {{LoginLink}}, {{SchoolName}},
    -- {{TempPassword}}
    IsActive    BIT DEFAULT 1,
    CreationDate DATETIME DEFAULT GETUTCDATE(),
    ModifiedDate DATETIME DEFAULT GETUTCDATE()
)
------------------------------------------------------------

ALTER TABLE School
ADD LogoUrl NVARCHAR(MAX) NULL,
    LogoPublicId NVARCHAR(500) NULL;

