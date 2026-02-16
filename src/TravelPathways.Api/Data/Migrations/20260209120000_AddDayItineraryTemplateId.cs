using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDayItineraryTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ItineraryTemplateId",
                table: "DayItineraries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayItineraries_ItineraryTemplateId",
                table: "DayItineraries",
                column: "ItineraryTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_DayItineraries_DestinationMaster_ItineraryTemplateId",
                table: "DayItineraries",
                column: "ItineraryTemplateId",
                principalTable: "DestinationMaster",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayItineraries_DestinationMaster_ItineraryTemplateId",
                table: "DayItineraries");

            migrationBuilder.DropIndex(
                name: "IX_DayItineraries_ItineraryTemplateId",
                table: "DayItineraries");

            migrationBuilder.DropColumn(
                name: "ItineraryTemplateId",
                table: "DayItineraries");
        }
    }
}
