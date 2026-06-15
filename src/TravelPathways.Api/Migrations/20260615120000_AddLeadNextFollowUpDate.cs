using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPathways.Api.Migrations
{
    [Migration("20260615120000_AddLeadNextFollowUpDate")]
    public partial class AddLeadNextFollowUpDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NextFollowUpDate",
                table: "Leads",
                type: "date",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NextFollowUpDate", table: "Leads");
        }
    }
}
