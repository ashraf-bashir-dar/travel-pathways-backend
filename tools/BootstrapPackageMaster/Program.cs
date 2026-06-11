using Npgsql;

const string sql = """
CREATE TABLE IF NOT EXISTS "PackageInclusionMasters" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAtUtc" timestamp with time zone,
    "TenantId" uuid NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "Code" character varying(64) NOT NULL,
    "Label" character varying(500) NOT NULL,
    "SortOrder" integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_PackageInclusionMasters" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PackageInclusionMasters_TenantId_Code" ON "PackageInclusionMasters" ("TenantId", "Code");
CREATE INDEX IF NOT EXISTS "IX_PackageInclusionMasters_TenantId_SortOrder" ON "PackageInclusionMasters" ("TenantId", "SortOrder");

CREATE TABLE IF NOT EXISTS "PackageLocationMasters" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAtUtc" timestamp with time zone,
    "TenantId" uuid NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "Name" character varying(200) NOT NULL,
    "AllowPickup" boolean NOT NULL DEFAULT true,
    "AllowDrop" boolean NOT NULL DEFAULT true,
    "SortOrder" integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_PackageLocationMasters" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PackageLocationMasters_TenantId_Name" ON "PackageLocationMasters" ("TenantId", "Name");
CREATE INDEX IF NOT EXISTS "IX_PackageLocationMasters_TenantId_SortOrder" ON "PackageLocationMasters" ("TenantId", "SortOrder");
""";

var connStr = args.Length > 0
    ? args[0]
    : "Host=localhost;Port=5432;Database=TravelPathways;Username=postgres;Password=Admin@123*";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand(sql, conn);
await cmd.ExecuteNonQueryAsync();
Console.WriteLine("Package master tables ensured.");
