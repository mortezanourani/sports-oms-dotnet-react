using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261124043800_AddOrgUnitTypeAndScope")]
    public partial class AddOrgUnitTypeAndScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "org_units",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "org_units",
                type: "text",
                nullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scope",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "type",
                table: "org_units");
        }
    }
}
