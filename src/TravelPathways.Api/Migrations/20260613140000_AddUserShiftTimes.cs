using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260613140000_AddUserShiftTimes")]
    public partial class AddUserShiftTimes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ShiftStartTime",
                table: "Users",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ShiftEndTime",
                table: "Users",
                type: "time without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShiftStartTime", table: "Users");
            migrationBuilder.DropColumn(name: "ShiftEndTime", table: "Users");
        }
    }
}
