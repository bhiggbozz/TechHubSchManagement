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
