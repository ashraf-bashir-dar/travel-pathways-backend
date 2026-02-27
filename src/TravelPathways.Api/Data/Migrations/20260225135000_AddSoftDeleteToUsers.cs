using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Data.Migrations
{
    /// <summary>Adds soft-delete columns to Users table. EntityBase expects these; run before or with other pending migrations.</summary>
    [Migration("20260225135000_AddSoftDeleteToUsers")]
    public partial class AddSoftDeleteToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsDeleted", table: "Users");
            migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "Users");
        }
    }
}
