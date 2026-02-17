using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayItineraries_DestinationMaster_ItineraryTemplateId",
                table: "DayItineraries");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RoomCategory",
                table: "AccommodationRates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DayItineraries_DestinationMaster_ItineraryTemplateId",
                table: "DayItineraries",
                column: "ItineraryTemplateId",
                principalTable: "DestinationMaster",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayItineraries_DestinationMaster_ItineraryTemplateId",
                table: "DayItineraries");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "RoomCategory",
                table: "AccommodationRates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DayItineraries_DestinationMaster_ItineraryTemplateId",
                table: "DayItineraries",
                column: "ItineraryTemplateId",
                principalTable: "DestinationMaster",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
