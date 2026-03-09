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
