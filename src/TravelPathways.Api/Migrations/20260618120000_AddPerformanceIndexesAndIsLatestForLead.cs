using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260618120000_AddPerformanceIndexesAndIsLatestForLead")]
    public partial class AddPerformanceIndexesAndIsLatestForLead : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLatestForLead",
                table: "Packages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_TenantId_LeadId_IsLatestForLead",
                table: "Packages",
                columns: new[] { "TenantId", "LeadId", "IsLatestForLead" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_AssignedToUserId",
                table: "Leads",
                columns: new[] { "TenantId", "AssignedToUserId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_NextFollowUpDate",
                table: "Leads",
                columns: new[] { "TenantId", "NextFollowUpDate" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_Status_CreatedAt",
                table: "Leads",
                columns: new[] { "TenantId", "Status", "CreatedAt" },
                filter: "\"IsDeleted\" = false");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Leads_TenantId_Status_CreatedAt", table: "Leads");
            migrationBuilder.DropIndex(name: "IX_Leads_TenantId_NextFollowUpDate", table: "Leads");
            migrationBuilder.DropIndex(name: "IX_Leads_TenantId_AssignedToUserId", table: "Leads");
            migrationBuilder.DropIndex(name: "IX_Packages_TenantId_LeadId_IsLatestForLead", table: "Packages");
            migrationBuilder.DropColumn(name: "IsLatestForLead", table: "Packages");
        }
    }
}
