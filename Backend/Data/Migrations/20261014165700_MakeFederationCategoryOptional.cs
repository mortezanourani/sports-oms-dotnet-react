using Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261014165700_MakeFederationCategoryOptional")]
    public partial class MakeFederationCategoryOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "federations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE federations SET category = 'General' WHERE category IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "federations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
