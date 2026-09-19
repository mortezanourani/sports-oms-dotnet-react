using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMedalWithAgeGroupAndLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "age_group",
                table: "medals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "count",
                table: "medals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "field",
                table: "medals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "level",
                table: "medals",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "age_group",
                table: "medals");

            migrationBuilder.DropColumn(
                name: "count",
                table: "medals");

            migrationBuilder.DropColumn(
                name: "field",
                table: "medals");

            migrationBuilder.DropColumn(
                name: "level",
                table: "medals");
        }
    }
}
