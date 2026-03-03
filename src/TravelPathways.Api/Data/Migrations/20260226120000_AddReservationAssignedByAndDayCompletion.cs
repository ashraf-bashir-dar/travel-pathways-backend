using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Data.Migrations
{
    [Migration("20260226120000_AddReservationAssignedByAndDayCompletion")]
    public partial class AddReservationAssignedByAndDayCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedByUserId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservationDayCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationDayCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationDayCompletions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_ReservationDayCompletions_ReservationId", table: "ReservationDayCompletions", column: "ReservationId");
            migrationBuilder.CreateIndex(name: "IX_Reservations_AssignedByUserId", table: "Reservations", column: "AssignedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Users_AssignedByUserId",
                table: "Reservations",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<int>(
                name: "DayNumber",
                table: "ReservationPaymentScreenshots",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Reservations_Users_AssignedByUserId", table: "Reservations");
            migrationBuilder.DropTable(name: "ReservationDayCompletions");
            migrationBuilder.DropIndex(name: "IX_Reservations_AssignedByUserId", table: "Reservations");
            migrationBuilder.DropColumn(name: "AssignedByUserId", table: "Reservations");
            migrationBuilder.DropColumn(name: "DayNumber", table: "ReservationPaymentScreenshots");
        }
    }
}
