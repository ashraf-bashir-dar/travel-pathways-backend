-- Soft-remove legacy seeded PDF templates (classic-quote, modern-itinerary) from older installs.
-- Run once on PostgreSQL after removing built-in PDF layouts. Safe if rows already deleted.

UPDATE "PdfTemplates"
SET
  "IsDeleted" = TRUE,
  "DeletedAtUtc" = NOW(),
  "IsActive" = FALSE,
  "UpdatedAt" = NOW()
WHERE "Key" IN ('classic-quote', 'modern-itinerary')
  AND "IsDeleted" = FALSE;

-- Optional: clear tenant pointer if it still references a removed key (avoids confusing PDF errors)
UPDATE "Tenants" t
SET "PdfTemplateKey" = NULL
WHERE t."PdfTemplateKey" IN ('classic-quote', 'modern-itinerary');
