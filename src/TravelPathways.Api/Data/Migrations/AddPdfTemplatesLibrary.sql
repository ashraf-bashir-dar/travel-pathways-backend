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

INSERT INTO "PdfTemplates"
("Id","Key","Name","Description","IsSystem","IsActive","HtmlTemplate","CreatedAt","UpdatedAt","IsDeleted","DeletedAtUtc")
SELECT gen_random_uuid(),'classic-quote','Classic Quote','System template: classic quote design',TRUE,TRUE,NULL,now(),now(),FALSE,NULL
WHERE NOT EXISTS (SELECT 1 FROM "PdfTemplates" WHERE "Key"='classic-quote');

INSERT INTO "PdfTemplates"
("Id","Key","Name","Description","IsSystem","IsActive","HtmlTemplate","CreatedAt","UpdatedAt","IsDeleted","DeletedAtUtc")
SELECT gen_random_uuid(),'modern-itinerary','Modern Itinerary','System template: modern itinerary design',TRUE,TRUE,NULL,now(),now(),FALSE,NULL
WHERE NOT EXISTS (SELECT 1 FROM "PdfTemplates" WHERE "Key"='modern-itinerary');
