-- Reference data: Jammu and Kashmir, city Srinagar, and common Kashmir / Jammu areas.
-- PostgreSQL (matches EF Core InitialCreate: uuid, timestamptz, quoted identifiers).
-- Safe to run multiple times (idempotent).
--
-- Spelling notes:
--   - State name in script: "Jammu and Kashmir" (you wrote "Jammau" — assumed typo).
--   - "Pehalgam" → standard spelling "Pahalgam".
--   - "Guraz" → standard spelling "Gurez" (change the literal in the INSERT if you need the exact UI label "Guraz").
--
-- If you already have state "Jammu & Kashmir" from app seed and do NOT want a second state row,
-- comment out the first INSERT block; the city insert still attaches Srinagar to whichever state exists.

-- 1) State: "Jammu and Kashmir" (unique on "States"."Name")
INSERT INTO "States" ("Id", "Name", "Code", "DisplayOrder", "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAtUtc")
SELECT gen_random_uuid(),
       'Jammu and Kashmir',
       'JK',
       COALESCE((SELECT MAX("DisplayOrder") + 1 FROM "States" WHERE "IsDeleted" = FALSE), 1),
       now(),
       now(),
       FALSE,
       NULL
WHERE NOT EXISTS (
    SELECT 1 FROM "States" s
    WHERE s."IsDeleted" = FALSE AND s."Name" = 'Jammu and Kashmir'
);

-- 2) City: Srinagar under Jammu and Kashmir (or existing Jammu & Kashmir from seed)
INSERT INTO "Cities" ("Id", "Name", "StateId", "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAtUtc")
SELECT gen_random_uuid(),
       'Srinagar',
       s."Id",
       now(),
       now(),
       FALSE,
       NULL
FROM (
    SELECT "Id"
    FROM "States"
    WHERE "IsDeleted" = FALSE
      AND "Name" IN ('Jammu and Kashmir', 'Jammu & Kashmir')
    ORDER BY CASE WHEN "Name" = 'Jammu and Kashmir' THEN 0 ELSE 1 END
    LIMIT 1
) s
WHERE NOT EXISTS (
    SELECT 1
    FROM "Cities" c
    WHERE c."IsDeleted" = FALSE
      AND c."Name" = 'Srinagar'
      AND c."StateId" = s."Id"
);

-- 3) Areas (global list; unique on "Areas"."Name")
INSERT INTO "Areas" ("Id", "Name", "DisplayOrder", "CreatedAt", "UpdatedAt", "IsDeleted", "DeletedAtUtc")
SELECT gen_random_uuid(), v."Name", v."DisplayOrder", now(), now(), FALSE, NULL
FROM (VALUES
    ('Srinagar', 1),
    ('Sonmarg', 2),
    ('Gulmarg', 3),
    ('Pahalgam', 4),
    ('Katra', 5),
    ('Jammu', 6),
    ('Gurez', 7)
) AS v("Name", "DisplayOrder")
WHERE NOT EXISTS (
    SELECT 1 FROM "Areas" a WHERE a."IsDeleted" = FALSE AND a."Name" = v."Name"
);
