using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Data.Migrations
{
    [Migration("20260225170000_AddTenantPdfPreferences")]
    public partial class AddTenantPdfPreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "PdfCoverTitle", table: "Tenants", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PdfPrimaryColor", table: "Tenants", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PdfSecondaryColor", table: "Tenants", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "PdfShowBankDetails", table: "Tenants", type: "bit", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "PdfShowQrCodes", table: "Tenants", type: "bit", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PdfCoverTitle", table: "Tenants");
            migrationBuilder.DropColumn(name: "PdfPrimaryColor", table: "Tenants");
            migrationBuilder.DropColumn(name: "PdfSecondaryColor", table: "Tenants");
            migrationBuilder.DropColumn(name: "PdfShowBankDetails", table: "Tenants");
            migrationBuilder.DropColumn(name: "PdfShowQrCodes", table: "Tenants");
        }
    }
}
