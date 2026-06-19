using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260619120000_AddDriverVehicleFields")]
    public partial class AddDriverVehicleFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VehicleNumber",
                table: "Drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleModel",
                table: "Drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleImageUrl",
                table: "Drivers",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VehicleNumber", table: "Drivers");
            migrationBuilder.DropColumn(name: "VehicleModel", table: "Drivers");
            migrationBuilder.DropColumn(name: "VehicleImageUrl", table: "Drivers");
        }
    }
}
