using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    public partial class AddTenantPdfPolicyLists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SupplementCosts",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SupplementCosts",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "Tenants");
        }
    }
}
