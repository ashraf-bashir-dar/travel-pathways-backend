-- Fix: 42703 column p.MarginAmount does not exist
-- Run this script on your PostgreSQL database used by the API (same as ConnectionStrings).
-- Matches EF migration: 20260510070000_AddMarginAmountToPackages

ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "MarginAmount" numeric(18,2) NOT NULL DEFAULT 0;

-- If you apply SQL manually (not `dotnet ef database update`), register the migration so EF does not try to add the column again:
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260510070000_AddMarginAmountToPackages', '8.0.10'
WHERE NOT EXISTS (
  SELECT 1 FROM "__EFMigrationsHistory" h WHERE h."MigrationId" = '20260510070000_AddMarginAmountToPackages'
);
