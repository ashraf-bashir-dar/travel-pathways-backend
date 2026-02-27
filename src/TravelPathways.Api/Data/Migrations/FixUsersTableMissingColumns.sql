-- Run this script if you get "Invalid column name" for Users table columns.
-- Use when: dotnet ef database update says "already up to date" but the app still fails, or you prefer to fix the schema manually.

-- 1. Soft-delete columns (EntityBase)
IF COL_LENGTH('dbo.Users', 'IsDeleted') IS NULL
  ALTER TABLE [dbo].[Users] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Users', 'DeletedAtUtc') IS NULL
  ALTER TABLE [dbo].[Users] ADD [DeletedAtUtc] datetime2 NULL;

-- 2. Employee/details columns (AddEmployeeDetailsToUsers)
IF COL_LENGTH('dbo.Users', 'Phone') IS NULL
  ALTER TABLE [dbo].[Users] ADD [Phone] nvarchar(max) NULL;
IF COL_LENGTH('dbo.Users', 'DateOfBirth') IS NULL
  ALTER TABLE [dbo].[Users] ADD [DateOfBirth] datetime2 NULL;
IF COL_LENGTH('dbo.Users', 'JoinDate') IS NULL
  ALTER TABLE [dbo].[Users] ADD [JoinDate] datetime2 NULL;
IF COL_LENGTH('dbo.Users', 'Designation') IS NULL
  ALTER TABLE [dbo].[Users] ADD [Designation] nvarchar(max) NULL;
IF COL_LENGTH('dbo.Users', 'Address') IS NULL
  ALTER TABLE [dbo].[Users] ADD [Address] nvarchar(max) NULL;
IF COL_LENGTH('dbo.Users', 'EmergencyContactName') IS NULL
  ALTER TABLE [dbo].[Users] ADD [EmergencyContactName] nvarchar(max) NULL;
IF COL_LENGTH('dbo.Users', 'EmergencyContactPhone') IS NULL
  ALTER TABLE [dbo].[Users] ADD [EmergencyContactPhone] nvarchar(max) NULL;
IF COL_LENGTH('dbo.Users', 'ProfilePhotoUrl') IS NULL
  ALTER TABLE [dbo].[Users] ADD [ProfilePhotoUrl] nvarchar(max) NULL;
GO
