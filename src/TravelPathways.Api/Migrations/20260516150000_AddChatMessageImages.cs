using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260516150000_AddChatMessageImages")]
    public partial class AddChatMessageImages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ChatMessages" ADD COLUMN IF NOT EXISTS "ImageUrls" text NOT NULL DEFAULT '[]';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ChatMessages" DROP COLUMN IF EXISTS "ImageUrls";
                """);
        }
    }
}
