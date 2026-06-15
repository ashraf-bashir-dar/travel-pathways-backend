using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260617120000_AddEmployeeAssignedTaskAssigneeNotes")]
    public partial class AddEmployeeAssignedTaskAssigneeNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeNotes",
                table: "EmployeeAssignedTasks",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssigneeNotes",
                table: "EmployeeAssignedTasks");
        }
    }
}
