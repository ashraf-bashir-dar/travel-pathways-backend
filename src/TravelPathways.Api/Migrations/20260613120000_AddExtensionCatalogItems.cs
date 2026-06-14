using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations;

[Migration("20260613120000_AddExtensionCatalogItems")]
public partial class AddExtensionCatalogItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExtensionCatalogItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Details = table.Column<string>(type: "text", nullable: false),
                Icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "🧩"),
                SupportedBrowsers = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ChromeStoreUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                EdgeStoreUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DownloadApiPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                InstallSteps = table.Column<string>(type: "text", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExtensionCatalogItems", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExtensionCatalogItems_TenantId_Code",
            table: "ExtensionCatalogItems",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExtensionCatalogItems_TenantId_SortOrder",
            table: "ExtensionCatalogItems",
            columns: new[] { "TenantId", "SortOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExtensionCatalogItems");
    }
}
