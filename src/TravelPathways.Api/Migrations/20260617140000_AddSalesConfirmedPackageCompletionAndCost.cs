using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260617140000_AddSalesConfirmedPackageCompletionAndCost")]
    public partial class AddSalesConfirmedPackageCompletionAndCost : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalPackageCost",
                table: "SalesConfirmedPackages",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TourPackageId",
                table: "SalesConfirmedPackages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "SalesConfirmedPackages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FinalReview",
                table: "SalesConfirmedPackages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "SalesConfirmedPackages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByUserId",
                table: "SalesConfirmedPackages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesConfirmedPackages_TourPackageId",
                table: "SalesConfirmedPackages",
                column: "TourPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesConfirmedPackages_Packages_TourPackageId",
                table: "SalesConfirmedPackages",
                column: "TourPackageId",
                principalTable: "Packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesConfirmedPackages_Packages_TourPackageId",
                table: "SalesConfirmedPackages");

            migrationBuilder.DropIndex(
                name: "IX_SalesConfirmedPackages_TourPackageId",
                table: "SalesConfirmedPackages");

            migrationBuilder.DropColumn(name: "TotalPackageCost", table: "SalesConfirmedPackages");
            migrationBuilder.DropColumn(name: "TourPackageId", table: "SalesConfirmedPackages");
            migrationBuilder.DropColumn(name: "IsCompleted", table: "SalesConfirmedPackages");
            migrationBuilder.DropColumn(name: "FinalReview", table: "SalesConfirmedPackages");
            migrationBuilder.DropColumn(name: "CompletedAt", table: "SalesConfirmedPackages");
            migrationBuilder.DropColumn(name: "CompletedByUserId", table: "SalesConfirmedPackages");
        }
    }
}
