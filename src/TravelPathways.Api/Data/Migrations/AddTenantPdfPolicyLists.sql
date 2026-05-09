-- Add tenant-managed PDF policy lists (JSON stored as text)
-- PostgreSQL

ALTER TABLE "Tenants"
  ADD COLUMN IF NOT EXISTS "TermsAndConditions" text NOT NULL DEFAULT '[]';

ALTER TABLE "Tenants"
  ADD COLUMN IF NOT EXISTS "CancellationPolicy" text NOT NULL DEFAULT '[]';

ALTER TABLE "Tenants"
  ADD COLUMN IF NOT EXISTS "SupplementCosts" text NOT NULL DEFAULT '[]';
