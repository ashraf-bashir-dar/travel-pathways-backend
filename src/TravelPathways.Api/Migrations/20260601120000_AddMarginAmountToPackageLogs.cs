using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260601120000_AddMarginAmountToPackageLogs")]
    public partial class AddMarginAmountToPackageLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "PackageLogs"
                    ADD COLUMN IF NOT EXISTS "MarginAmount" numeric(18,2) NOT NULL DEFAULT 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "PackageLogs" DROP COLUMN IF EXISTS "MarginAmount";
                """);
        }
    }
}
