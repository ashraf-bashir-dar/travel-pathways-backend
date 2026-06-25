using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260625120000_AddEmployeeLifecycleStatus")]
    public partial class AddEmployeeLifecycleStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "Users",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LifecycleStatus", table: "Users");
        }
    }
}
