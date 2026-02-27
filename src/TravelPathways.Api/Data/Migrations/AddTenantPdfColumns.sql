-- Add PDF preference columns to Tenants table (per-tenant PDF look).
-- Run this if the EF migration AddTenantPdfPreferences has not been applied.
-- Safe to run multiple times: only adds columns that are missing.

IF OBJECT_ID('dbo.Tenants', 'U') IS NULL
  RETURN;

IF COL_LENGTH('dbo.Tenants', 'PdfCoverTitle') IS NULL
  ALTER TABLE [dbo].[Tenants] ADD [PdfCoverTitle] nvarchar(max) NULL;

IF COL_LENGTH('dbo.Tenants', 'PdfPrimaryColor') IS NULL
  ALTER TABLE [dbo].[Tenants] ADD [PdfPrimaryColor] nvarchar(max) NULL;

IF COL_LENGTH('dbo.Tenants', 'PdfSecondaryColor') IS NULL
  ALTER TABLE [dbo].[Tenants] ADD [PdfSecondaryColor] nvarchar(max) NULL;

IF COL_LENGTH('dbo.Tenants', 'PdfShowBankDetails') IS NULL
  ALTER TABLE [dbo].[Tenants] ADD [PdfShowBankDetails] bit NULL;

IF COL_LENGTH('dbo.Tenants', 'PdfShowQrCodes') IS NULL
  ALTER TABLE [dbo].[Tenants] ADD [PdfShowQrCodes] bit NULL;
GO
