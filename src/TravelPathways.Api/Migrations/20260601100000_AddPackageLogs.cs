using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260601100000_AddPackageLogs")]
    public partial class AddPackageLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'PackageClientSendLogs'
                    ) THEN
                        ALTER TABLE "PackageClientSendLogs" RENAME TO "PackageLogs";
                        ALTER TABLE "PackageLogs" RENAME COLUMN "SentByUserId" TO "ChangedByUserId";
                        ALTER TABLE "PackageLogs" RENAME COLUMN "SentByDisplayName" TO "ChangedByDisplayName";
                        ALTER TABLE "PackageLogs" DROP COLUMN IF EXISTS "Notes";
                        ALTER TABLE "PackageLogs" ADD COLUMN IF NOT EXISTS "Action" text NOT NULL DEFAULT 'Updated';
                        ALTER TABLE "PackageLogs" ADD COLUMN IF NOT EXISTS "Status" text NOT NULL DEFAULT 'New';
                    ELSIF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'PackageLogs'
                    ) THEN
                        CREATE TABLE "PackageLogs" (
                            "Id" uuid NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            "IsDeleted" boolean NOT NULL DEFAULT false,
                            "DeletedAtUtc" timestamp with time zone,
                            "TenantId" uuid NOT NULL,
                            "IsActive" boolean NOT NULL DEFAULT true,
                            "LeadId" uuid NOT NULL,
                            "PackageId" uuid NOT NULL,
                            "Action" text NOT NULL DEFAULT 'Updated',
                            "PackageName" text NOT NULL DEFAULT '',
                            "FinalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                            "MarginAmount" numeric(18,2) NOT NULL DEFAULT 0,
                            "Status" text NOT NULL DEFAULT 'New',
                            "ChangedByUserId" uuid,
                            "ChangedByDisplayName" text NOT NULL DEFAULT '',
                            CONSTRAINT "PK_PackageLogs" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_PackageLogs_Leads_LeadId"
                                FOREIGN KEY ("LeadId") REFERENCES "Leads" ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_PackageLogs_Packages_PackageId"
                                FOREIGN KEY ("PackageId") REFERENCES "Packages" ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_PackageLogs_Users_ChangedByUserId"
                                FOREIGN KEY ("ChangedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
                        );
                    END IF;
                END $$;

                CREATE INDEX IF NOT EXISTS "IX_PackageLogs_LeadId_CreatedAt"
                    ON "PackageLogs" ("LeadId", "CreatedAt" DESC);
                CREATE INDEX IF NOT EXISTS "IX_PackageLogs_PackageId_CreatedAt"
                    ON "PackageLogs" ("PackageId", "CreatedAt" DESC);
                CREATE INDEX IF NOT EXISTS "IX_PackageLogs_TenantId_LeadId"
                    ON "PackageLogs" ("TenantId", "LeadId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PackageLogs");
        }
    }
}
