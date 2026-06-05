using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260525173000_AddReservationHotelBookings")]
    public partial class AddReservationHotelBookings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "ReservationHotelBookings" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "DeletedAtUtc" timestamp with time zone,
                    "TenantId" uuid NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "ReservationId" uuid NOT NULL,
                    "DayNumber" integer NOT NULL,
                    "BookingDate" timestamp with time zone NOT NULL,
                    "CheckInDate" timestamp with time zone,
                    "CheckOutDate" timestamp with time zone,
                    "HotelId" uuid,
                    "HotelName" text NOT NULL DEFAULT '',
                    "IsHouseboat" boolean NOT NULL DEFAULT false,
                    "RoomType" text,
                    "NumberOfRooms" integer NOT NULL DEFAULT 0,
                    "ExtraBedCount" integer NOT NULL DEFAULT 0,
                    "CnbCount" integer NOT NULL DEFAULT 0,
                    "NumberOfPersons" integer NOT NULL DEFAULT 0,
                    "RatePerNight" numeric(18,2) NOT NULL DEFAULT 0,
                    "TotalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "AdvancePaid" numeric(18,2) NOT NULL DEFAULT 0,
                    "BalanceAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "Status" text NOT NULL DEFAULT 'Pending',
                    "ConfirmationNumber" text,
                    "Notes" text,
                    CONSTRAINT "PK_ReservationHotelBookings" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ReservationHotelBookings_Reservations_ReservationId"
                        FOREIGN KEY ("ReservationId") REFERENCES "Reservations" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_ReservationHotelBookings_ReservationId_DayNumber"
                    ON "ReservationHotelBookings" ("ReservationId", "DayNumber");

                CREATE TABLE IF NOT EXISTS "ReservationHotelBookingDocuments" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "DeletedAtUtc" timestamp with time zone,
                    "ReservationHotelBookingId" uuid NOT NULL,
                    "Type" text NOT NULL DEFAULT 'PaymentProof',
                    "Amount" numeric(18,2),
                    "PaymentDate" timestamp with time zone,
                    "FileUrl" text NOT NULL,
                    "FileName" text NOT NULL,
                    CONSTRAINT "PK_ReservationHotelBookingDocuments" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ReservationHotelBookingDocuments_ReservationHotelBookings_ReservationHotelBookingId"
                        FOREIGN KEY ("ReservationHotelBookingId") REFERENCES "ReservationHotelBookings" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_ReservationHotelBookingDocuments_ReservationHotelBookingId"
                    ON "ReservationHotelBookingDocuments" ("ReservationHotelBookingId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReservationHotelBookingDocuments");
            migrationBuilder.DropTable(name: "ReservationHotelBookings");
        }
    }
}
