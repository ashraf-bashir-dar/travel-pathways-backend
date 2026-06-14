using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations;

[Migration("20260613100000_AddUserActivityPageVisitSource")]
public partial class AddUserActivityPageVisitSource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "UserActivityPageVisits",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "InApp");

        migrationBuilder.AddColumn<int>(
            name: "DurationSeconds",
            table: "UserActivityPageVisits",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserActivityPageVisits_TenantId_Source_VisitedAtUtc",
            table: "UserActivityPageVisits",
            columns: new[] { "TenantId", "Source", "VisitedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserActivityPageVisits_TenantId_Source_VisitedAtUtc",
            table: "UserActivityPageVisits");

        migrationBuilder.DropColumn(name: "Source", table: "UserActivityPageVisits");
        migrationBuilder.DropColumn(name: "DurationSeconds", table: "UserActivityPageVisits");
    }
}
