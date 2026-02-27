using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Data.Migrations
{
    [Migration("20260225140000_AddEmployeeDetailsToUsers")]
    public partial class AddEmployeeDetailsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinDate",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Designation",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoUrl",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Phone", table: "Users");
            migrationBuilder.DropColumn(name: "DateOfBirth", table: "Users");
            migrationBuilder.DropColumn(name: "JoinDate", table: "Users");
            migrationBuilder.DropColumn(name: "Designation", table: "Users");
            migrationBuilder.DropColumn(name: "Address", table: "Users");
            migrationBuilder.DropColumn(name: "EmergencyContactName", table: "Users");
            migrationBuilder.DropColumn(name: "EmergencyContactPhone", table: "Users");
            migrationBuilder.DropColumn(name: "ProfilePhotoUrl", table: "Users");
        }
    }
}
