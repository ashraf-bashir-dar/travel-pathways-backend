using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations;

[Migration("20260605100000_AddCallLogs")]
public partial class AddCallLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "CallLogs" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "UserId" uuid,
                "Direction" character varying(16) NOT NULL,
                "Status" character varying(64),
                "Provider" character varying(64),
                "ProviderCallId" character varying(128),
                "FromNumber" character varying(48),
                "ToNumber" character varying(48),
                "StartedAtUtc" timestamp with time zone,
                "EndedAtUtc" timestamp with time zone,
                "DurationSeconds" integer,
                "RawPayload" text NOT NULL DEFAULT '{}',
                CONSTRAINT "PK_CallLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CallLogs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_CallLogs_TenantId_CreatedAt" ON "CallLogs" ("TenantId", "CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_CallLogs_UserId_CreatedAt" ON "CallLogs" ("UserId", "CreatedAt" DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "CallLogs";""");
    }
}
