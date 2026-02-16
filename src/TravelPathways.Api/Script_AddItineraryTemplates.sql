-- Creates DestinationMaster table and records migrations (for a DB that has neither ItineraryTemplates nor DestinationMaster).
-- Use Script_EnsureDestinationMaster.sql if you're not sure (it handles both rename and create).

BEGIN TRANSACTION;
GO

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

COMMIT;
GO
