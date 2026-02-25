-- Add soft-delete columns (IsDeleted, DeletedAtUtc) to all tables that inherit from EntityBase.
-- Run this against your database if the EF migration AddSoftDeleteAndDeactivateUsers has not been applied.
-- After running, optionally run: dotnet ef migrations add AddSoftDeleteAndDeactivateUsers --context AppDbContext
-- so the snapshot matches (stop IIS/Visual Studio first if the build locks the DLL).

DECLARE @tables TABLE (Name sysname);
INSERT INTO @tables (Name) VALUES
 (N'Tenants'), (N'TenantDocuments'), (N'TenantBankAccounts'), (N'TenantQrCodes'),
 (N'Users'), (N'Plans'), (N'PlanPrices'), (N'Leads'), (N'LeadFollowUps'),
 (N'Hotels'), (N'AccommodationRates'), (N'TransportCompanies'), (N'Vehicles'), (N'VehiclePricing'),
 (N'Packages'), (N'DayItineraries'), (N'DestinationMaster'), (N'States'), (N'Cities'), (N'Areas'), (N'Payments');

DECLARE @t sysname, @sql nvarchar(max);
DECLARE c CURSOR FOR SELECT Name FROM @tables;
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
  IF COL_LENGTH(@t, N'IsDeleted') IS NULL
  BEGIN
    SET @sql = N'ALTER TABLE [dbo].[' + @t + N'] ADD [IsDeleted] bit NOT NULL DEFAULT 0;';
    EXEC sp_executesql @sql;
  END
  IF COL_LENGTH(@t, N'DeletedAtUtc') IS NULL
  BEGIN
    SET @sql = N'ALTER TABLE [dbo].[' + @t + N'] ADD [DeletedAtUtc] datetime2 NULL;';
    EXEC sp_executesql @sql;
  END
  FETCH NEXT FROM c INTO @t;
END
CLOSE c;
DEALLOCATE c;
GO
