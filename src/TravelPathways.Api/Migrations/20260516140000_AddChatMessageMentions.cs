using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260516140000_AddChatMessageMentions")]
    public partial class AddChatMessageMentions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ChatMessages" ADD COLUMN IF NOT EXISTS "MentionedUserIds" text NOT NULL DEFAULT '[]';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ChatMessages" DROP COLUMN IF EXISTS "MentionedUserIds";
                """);
        }
    }
}
