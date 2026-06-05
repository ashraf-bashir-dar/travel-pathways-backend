using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260601130000_AddSnapshotJsonToPackageLogs")]
    public partial class AddSnapshotJsonToPackageLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "PackageLogs"
                    ADD COLUMN IF NOT EXISTS "SnapshotJson" text NOT NULL DEFAULT '{}';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "PackageLogs" DROP COLUMN IF EXISTS "SnapshotJson";
                """);
        }
    }
}
