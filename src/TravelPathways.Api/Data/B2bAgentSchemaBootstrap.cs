using Microsoft.EntityFrameworkCore;

namespace TravelPathways.Api.Data;

internal static class B2bAgentSchemaBootstrap
{
    private const string Sql = """
        CREATE TABLE IF NOT EXISTS "B2bAgents" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "IsDeleted" boolean NOT NULL DEFAULT false,
            "DeletedAtUtc" timestamp with time zone,
            "TenantId" uuid NOT NULL,
            "IsActive" boolean NOT NULL DEFAULT true,
            "Name" character varying(200) NOT NULL,
            "ContactPerson" character varying(200) NOT NULL,
            "ContactNumber1" character varying(32),
            "ContactNumber2" character varying(32),
            "Email" character varying(256),
            "WebsiteUrl" character varying(500),
            "State" character varying(120),
            "City" character varying(120),
            "Country" character varying(120),
            "PinCode" character varying(16),
            CONSTRAINT "PK_B2bAgents" PRIMARY KEY ("Id")
        );
        CREATE INDEX IF NOT EXISTS "IX_B2bAgents_TenantId_Name" ON "B2bAgents" ("TenantId", "Name");
        CREATE INDEX IF NOT EXISTS "IX_B2bAgents_TenantId_IsActive" ON "B2bAgents" ("TenantId", "IsActive");

        CREATE TABLE IF NOT EXISTS "B2bAgentDocuments" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "IsDeleted" boolean NOT NULL DEFAULT false,
            "DeletedAtUtc" timestamp with time zone,
            "B2bAgentId" uuid NOT NULL,
            "FileName" character varying(260) NOT NULL,
            "Url" character varying(1000) NOT NULL,
            CONSTRAINT "PK_B2bAgentDocuments" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_B2bAgentDocuments_B2bAgents_B2bAgentId" FOREIGN KEY ("B2bAgentId") REFERENCES "B2bAgents" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_B2bAgentDocuments_B2bAgentId" ON "B2bAgentDocuments" ("B2bAgentId");
        """;

    private static int _ensured;

    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
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
