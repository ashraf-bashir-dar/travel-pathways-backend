using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260510070000_AddMarginAmountToPackages")]
    public partial class AddMarginAmountToPackages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS: safe when startup hotfix or manual SQL already added the column.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "MarginAmount" numeric(18,2) NOT NULL DEFAULT 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Packages" DROP COLUMN IF EXISTS "MarginAmount";
                """);
        }
    }
}
