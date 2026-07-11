-- =====================================================
-- Platform Users (application-level Admins)
-- Separate from school Users table.
-- PlatformSuperAdmin is seeded here; PlatformAdmins are
-- created via the API by PlatformSuperAdmin.
-- =====================================================

CREATE TABLE PlatformUser (
    Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    FirstName    NVARCHAR(100)    NOT NULL,
    LastName     NVARCHAR(100)    NOT NULL,
    Email        NVARCHAR(200)    NOT NULL,
    Username     NVARCHAR(100)    NOT NULL,
    PasswordHash NVARCHAR(500)    NOT NULL,
    Role         NVARCHAR(20)     NOT NULL DEFAULT 'PlatformAdmin',
    IsActive     BIT              NOT NULL DEFAULT 1,
    IsDeleted    BIT              NOT NULL DEFAULT 0,
    CreatedBy    UNIQUEIDENTIFIER NULL,
    CreatedAt    NVARCHAR(30)     NOT NULL,
    ModifiedAt   NVARCHAR(30)     NOT NULL
);

CREATE UNIQUE INDEX IX_PlatformUsers_Username
    ON PlatformUser(Username) WHERE IsDeleted = 0;

CREATE UNIQUE INDEX IX_PlatformUsers_Email
    ON PlatformUser(Email) WHERE IsDeleted = 0;

-- Seed the first PlatformSuperAdmin
-- Username: platformadmin
-- Password: Platform@123 (SHA256 hash)
INSERT INTO PlatformUser (Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive, IsDeleted, CreatedBy, CreatedAt, ModifiedAt)
VALUES (
    NEWID(),
    'Platform',
    'Admin',
    'platform@techhub.com',
    'platformadmin',
    '9A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A',
    'PlatformSuperAdmin',
    1,
    0,
    NULL,
    FORMAT(GETUTCDATE(), 'yyyy-MM-dd HH:mm:ss'),
    FORMAT(GETUTCDATE(), 'yyyy-MM-dd HH:mm:ss')
);
