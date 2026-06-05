using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260525223000_AddReservationHotelBookingLocks")]
    public partial class AddReservationHotelBookingLocks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ReservationHotelBookings"
                    ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ReservationHotelBookings"
                    DROP COLUMN IF EXISTS "IsLocked";
                """);
        }
    }
}
