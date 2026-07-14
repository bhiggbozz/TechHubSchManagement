-- Add MiddleName column to Users table
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[Users]') 
               AND name = 'MiddleName')
BEGIN
    ALTER TABLE Users
    ADD MiddleName NVARCHAR(100) NULL;
    
    PRINT 'MiddleName column added to Users table';
END
