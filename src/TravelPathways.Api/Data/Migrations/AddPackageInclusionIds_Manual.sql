-- Add InclusionIds column to Packages (for package Inclusions/Exclusions in PDF)
-- Database: TravelPathways (or your app's database name from appsettings.json)
-- Run in SQL Server Management Studio or: sqlcmd -S localhost -d TravelPathways -E -i AddPackageInclusionIds_Manual.sql

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Packages') AND name = 'InclusionIds'
)
BEGIN
    ALTER TABLE [dbo].[Packages]
    ADD [InclusionIds] nvarchar(max) NOT NULL DEFAULT '[]';
END
GO

-- Record that this migration was applied (so "dotnet ef database update" won't run it again)
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260212140000_AddPackageInclusionIds')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212140000_AddPackageInclusionIds', N'8.0.22');
END
GO
