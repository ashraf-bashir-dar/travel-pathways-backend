using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260616120000_AddEmployeeAssignedTasks")]
    public partial class AddEmployeeAssignedTasks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeAssignedTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAssignedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAssignedTasks_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAssignedTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssignedTasks_AssignedByUserId",
                table: "EmployeeAssignedTasks",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssignedTasks_AssignedToUserId",
                table: "EmployeeAssignedTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssignedTasks_TenantId_AssignedToUserId_DueDate",
                table: "EmployeeAssignedTasks",
                columns: new[] { "TenantId", "AssignedToUserId", "DueDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmployeeAssignedTasks");
        }
    }
}
