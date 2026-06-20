-- Keep only Client — Carbon & Lime PDF template. Soft-delete all others and assign all tenants to it.
-- Run after Patch-CarbonLimePdfTemplate.ps1 (or ensure pdf-client-15-carbon-lime row exists with HTML).
-- PostgreSQL

BEGIN;

-- Ensure Carbon & Lime is the only active template
UPDATE "PdfTemplates"
SET
  "IsDeleted" = FALSE,
  "DeletedAtUtc" = NULL,
  "IsActive" = TRUE,
  "UpdatedAt" = NOW()
WHERE "Key" = 'pdf-client-15-carbon-lime';

-- Assign every tenant to Carbon & Lime
UPDATE "Tenants"
SET "PdfTemplateKey" = 'pdf-client-15-carbon-lime'
WHERE "PdfTemplateKey" IS DISTINCT FROM 'pdf-client-15-carbon-lime';

-- Remove all other PDF templates from the library
UPDATE "PdfTemplates"
SET
  "IsDeleted" = TRUE,
  "DeletedAtUtc" = NOW(),
  "IsActive" = FALSE,
  "UpdatedAt" = NOW()
WHERE "Key" <> 'pdf-client-15-carbon-lime'
  AND "IsDeleted" = FALSE;

COMMIT;
