using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260618160000_AddDriversAndAssignments")]
    public partial class AddDriversAndAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    TransportCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LicenceNumber = table.Column<string>(type: "text", nullable: true),
                    AadharLastFour = table.Column<string>(type: "text", nullable: true),
                    LicenceDocumentUrl = table.Column<string>(type: "text", nullable: true),
                    AadharDocumentUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drivers_TransportCompanies_TransportCompanyId",
                        column: x => x.TransportCompanyId,
                        principalTable: "TransportCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PackageDriverAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransportCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleNumber = table.Column<string>(type: "text", nullable: false),
                    VehicleModel = table.Column<string>(type: "text", nullable: true),
                    VehicleImageUrl = table.Column<string>(type: "text", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceRating = table.Column<int>(type: "integer", nullable: true),
                    ServiceNotes = table.Column<string>(type: "text", nullable: true),
                    RatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDriverAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageDriverAssignments_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageDriverAssignments_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageDriverAssignments_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageDriverAssignments_TransportCompanies_TransportCompanyId",
                        column: x => x.TransportCompanyId,
                        principalTable: "TransportCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PackageDriverAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PackageDriverAssignments_Users_RatedByUserId",
                        column: x => x.RatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId_PhoneNumber",
                table: "Drivers",
                columns: new[] { "TenantId", "PhoneNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TransportCompanyId",
                table: "Drivers",
                column: "TransportCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDriverAssignments_DriverId",
                table: "PackageDriverAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDriverAssignments_PackageId",
                table: "PackageDriverAssignments",
                column: "PackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageDriverAssignments_ReservationId",
                table: "PackageDriverAssignments",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDriverAssignments_TransportCompanyId",
                table: "PackageDriverAssignments",
                column: "TransportCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDriverAssignments_AssignedByUserId",
                table: "PackageDriverAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDriverAssignments_RatedByUserId",
                table: "PackageDriverAssignments",
                column: "RatedByUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PackageDriverAssignments");
            migrationBuilder.DropTable(name: "Drivers");
        }
    }
}
