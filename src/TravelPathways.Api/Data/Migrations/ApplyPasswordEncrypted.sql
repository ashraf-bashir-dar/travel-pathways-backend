-- Add PasswordEncrypted column to Users (from migration AddPasswordEncrypted).
-- Run this against your database if the migration hasn't been applied (e.g. when build is locked).

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = 'PasswordEncrypted'
)
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordEncrypted] nvarchar(max) NULL;
END
GO
