-- PDF templates library for reusable tenant assignment
CREATE TABLE IF NOT EXISTS "PdfTemplates" (
    "Id" uuid NOT NULL,
    "Key" varchar(120) NOT NULL,
    "Name" varchar(180) NOT NULL,
    "Description" text NULL,
    "IsSystem" boolean NOT NULL DEFAULT FALSE,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "HtmlTemplate" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "DeletedAtUtc" timestamp with time zone NULL,
    CONSTRAINT "PK_PdfTemplates" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_PdfTemplates_Key" ON "PdfTemplates" ("Key");

-- Default HTML templates are no longer inserted: each PdfTemplates row must have non-empty HtmlTemplate.
-- Sample layout: Api/Data/DefaultHtmlTemplates/classic-quote-replica.html
