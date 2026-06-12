using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260611140000_AddHotelBookingCancellationReason")]
    public partial class AddHotelBookingCancellationReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "ReservationHotelBookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReasonDetail",
                table: "ReservationHotelBookings",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CancellationReason", table: "ReservationHotelBookings");
            migrationBuilder.DropColumn(name: "CancellationReasonDetail", table: "ReservationHotelBookings");
        }
    }
}
