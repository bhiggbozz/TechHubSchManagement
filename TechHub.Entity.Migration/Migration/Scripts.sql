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