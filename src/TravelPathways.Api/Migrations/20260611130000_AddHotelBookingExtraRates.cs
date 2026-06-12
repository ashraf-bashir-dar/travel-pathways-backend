using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260611130000_AddHotelBookingExtraRates")]
    public partial class AddHotelBookingExtraRates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExtraBedRate",
                table: "ReservationHotelBookings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CnbRate",
                table: "ReservationHotelBookings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExtraBedRate", table: "ReservationHotelBookings");
            migrationBuilder.DropColumn(name: "CnbRate", table: "ReservationHotelBookings");
        }
    }
}
