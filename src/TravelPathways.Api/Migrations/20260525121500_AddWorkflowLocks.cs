using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260525121500_AddWorkflowLocks")]
    public partial class AddWorkflowLocks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
                ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
                ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsLocked", table: "Reservations");
            migrationBuilder.DropColumn(name: "IsLocked", table: "Packages");
            migrationBuilder.DropColumn(name: "IsLocked", table: "Leads");
        }
    }
}
