using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260516120000_AddTeamChat")]
    public partial class AddTeamChat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "ChatGroups" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "Name" character varying(200) NOT NULL,
                    "Description" character varying(500),
                    "CreatedByUserId" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "DeletedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_ChatGroups" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ChatGroups_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_ChatGroups_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS "ChatGroupMembers" (
                    "GroupId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "JoinedAt" timestamp with time zone NOT NULL,
                    "AddedByUserId" uuid,
                    "LastReadAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_ChatGroupMembers" PRIMARY KEY ("GroupId", "UserId"),
                    CONSTRAINT "FK_ChatGroupMembers_ChatGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ChatGroups" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_ChatGroupMembers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE IF NOT EXISTS "ChatMessages" (
                    "Id" uuid NOT NULL,
                    "GroupId" uuid NOT NULL,
                    "SenderUserId" uuid NOT NULL,
                    "Body" character varying(4000) NOT NULL,
                    "SentAtUtc" timestamp with time zone NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "DeletedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ChatMessages_ChatGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ChatGroups" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_ChatMessages_Users_SenderUserId" FOREIGN KEY ("SenderUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
                );

                CREATE INDEX IF NOT EXISTS "IX_ChatGroups_TenantId" ON "ChatGroups" ("TenantId");
                CREATE INDEX IF NOT EXISTS "IX_ChatMessages_GroupId_SentAtUtc" ON "ChatMessages" ("GroupId", "SentAtUtc");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS "ChatMessages";
                DROP TABLE IF EXISTS "ChatGroupMembers";
                DROP TABLE IF EXISTS "ChatGroups";
                """);
        }
    }
}
