-- Ensures the DestinationMaster table exists. Run against your app database (e.g. TravelPathways on LocalDB).
-- If __EFMigrationsHistory is missing (new DB), it is created first.

-- 1) Ensure EF migrations history table exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END
GO

-- 2) Rename old table if it exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ItineraryTemplates')
   AND NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DestinationMaster')
BEGIN
    EXEC sp_rename 'ItineraryTemplates', 'DestinationMaster';
END
GO

-- 3) Create DestinationMaster if it still doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DestinationMaster')
BEGIN
    CREATE TABLE [DestinationMaster] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DestinationMaster] PRIMARY KEY ([Id])
    );
END
GO

-- 4) Record migrations so EF doesn't run them again
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260207120000_AddItineraryTemplates')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260207120000_AddItineraryTemplates', N'8.0.22');
END
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260207130000_RenameItineraryTemplatesToDestinationMaster')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260207130000_RenameItineraryTemplatesToDestinationMaster', N'8.0.22');
END
GO
