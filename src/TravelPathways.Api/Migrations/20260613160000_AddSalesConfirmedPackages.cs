using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260613160000_AddSalesConfirmedPackages")]
    public partial class AddSalesConfirmedPackages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesConfirmedPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ConfirmationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferenceContact = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesConfirmedPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesConfirmedPackages_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesConfirmedPackages_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesConfirmedPackages_LeadId",
                table: "SalesConfirmedPackages",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesConfirmedPackages_RecordedByUserId",
                table: "SalesConfirmedPackages",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesConfirmedPackages_TenantId_ConfirmationDate",
                table: "SalesConfirmedPackages",
                columns: new[] { "TenantId", "ConfirmationDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesConfirmedPackages");
        }
    }
}
