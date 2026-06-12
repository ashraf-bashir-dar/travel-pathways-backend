using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260612100000_AddUserActivityDailySummaries")]
    public partial class AddUserActivityDailySummaries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserActivityDailySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActiveSeconds = table.Column<int>(type: "integer", nullable: false),
                    IdleSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsCurrentlyIdle = table.Column<bool>(type: "boolean", nullable: false),
                    LastReportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityDailySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivityDailySummaries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityDailySummaries_TenantId_UserId_ActivityDate",
                table: "UserActivityDailySummaries",
                columns: new[] { "TenantId", "UserId", "ActivityDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityDailySummaries_UserId",
                table: "UserActivityDailySummaries",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserActivityDailySummaries");
        }
    }
}
