using Microsoft.EntityFrameworkCore;

namespace TravelPathways.Api.Data;

internal static class PackageMasterSchemaBootstrap
{
    private const string Sql = """
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
            "IsInclusion" boolean NOT NULL DEFAULT true,
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

        ALTER TABLE "PackageInclusionMasters" ADD COLUMN IF NOT EXISTS "IsInclusion" boolean NOT NULL DEFAULT true;
        """;

    private static int _ensured;

    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "PackageInclusionMasters" ADD COLUMN IF NOT EXISTS "IsInclusion" boolean NOT NULL DEFAULT true;""",
                ct);
        }
        catch
        {
            // Table may not exist yet; CREATE below will add the column for new installs.
        }

        if (Interlocked.CompareExchange(ref _ensured, 1, 0) == 1)
            return;

        try
        {
            await db.Database.ExecuteSqlRawAsync(Sql, ct);
        }
        catch
        {
            Interlocked.Exchange(ref _ensured, 0);
            throw;
        }
    }
}
