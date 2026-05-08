-- Add tenant-level PDF template selector
-- Run this script if your DB does not yet contain the column.

IF COL_LENGTH('dbo.Tenants', 'PdfTemplateKey') IS NULL
  ALTER TABLE [dbo].[Tenants] ADD [PdfTemplateKey] nvarchar(64) NULL;

