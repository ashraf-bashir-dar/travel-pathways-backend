using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260516130000_AddChatDirectMessages")]
    public partial class AddChatDirectMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ChatGroups" ADD COLUMN IF NOT EXISTS "IsDirect" boolean NOT NULL DEFAULT false;
                ALTER TABLE "ChatGroups" ADD COLUMN IF NOT EXISTS "DirectPairKey" character varying(100);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChatGroups_TenantId_DirectPairKey"
                    ON "ChatGroups" ("TenantId", "DirectPairKey")
                    WHERE "IsDirect" = true AND "DirectPairKey" IS NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_ChatGroups_TenantId_DirectPairKey";
                ALTER TABLE "ChatGroups" DROP COLUMN IF EXISTS "DirectPairKey";
                ALTER TABLE "ChatGroups" DROP COLUMN IF EXISTS "IsDirect";
                """);
        }
    }
}
