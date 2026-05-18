using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260516160000_AddInboundLeadIntegrations")]
    public partial class AddInboundLeadIntegrations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InboundLeadsFeatureEnabled",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ParticipateInInboundAutoAssign",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InboundDailyLeadQuota",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InboundAllowedLeadSources",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "InboundProvider",
                table: "Leads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundExternalId",
                table: "Leads",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantLeadIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboundKey = table.Column<string>(type: "text", nullable: false),
                    IsInboundEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoAssignEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MetaPageId = table.Column<string>(type: "text", nullable: true),
                    MetaPageAccessTokenEncrypted = table.Column<string>(type: "text", nullable: true),
                    MetaConnectionVerified = table.Column<bool>(type: "boolean", nullable: false),
                    MetaLastWebhookAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLeadIntegrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundLeadEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RawPayload = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundLeadEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLeadIntegrations_InboundKey",
                table: "TenantLeadIntegrations",
                column: "InboundKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantLeadIntegrations_TenantId",
                table: "TenantLeadIntegrations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_InboundProvider_InboundExternalId",
                table: "Leads",
                columns: new[] { "TenantId", "InboundProvider", "InboundExternalId" },
                unique: true,
                filter: "\"InboundExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InboundLeadEvents_TenantId_CreatedAt",
                table: "InboundLeadEvents",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InboundLeadEvents");
            migrationBuilder.DropTable(name: "TenantLeadIntegrations");
            migrationBuilder.DropIndex(name: "IX_Leads_TenantId_InboundProvider_InboundExternalId", table: "Leads");
            migrationBuilder.DropColumn(name: "InboundProvider", table: "Leads");
            migrationBuilder.DropColumn(name: "InboundExternalId", table: "Leads");
            migrationBuilder.DropColumn(name: "ParticipateInInboundAutoAssign", table: "Users");
            migrationBuilder.DropColumn(name: "InboundDailyLeadQuota", table: "Users");
            migrationBuilder.DropColumn(name: "InboundAllowedLeadSources", table: "Users");
            migrationBuilder.DropColumn(name: "InboundLeadsFeatureEnabled", table: "Tenants");
        }
    }
}
