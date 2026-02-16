-- Add RoomCategory column to AccommodationRates (run this if dotnet ef database update fails)
-- Database: TravelPathways (or your app's database name from appsettings.json)
-- Run in SQL Server Management Studio or: sqlcmd -S localhost -d TravelPathways -E -i AddRoomCategory_Manual.sql

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AccommodationRates') AND name = 'RoomCategory'
)
BEGIN
    ALTER TABLE [dbo].[AccommodationRates]
    ADD [RoomCategory] nvarchar(100) NULL;
END
GO

-- Record that this migration was applied (so "dotnet ef database update" won't run it again)
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260212120000_AddRoomCategoryToAccommodationRate')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212120000_AddRoomCategoryToAccommodationRate', N'8.0.22');
END
GO
