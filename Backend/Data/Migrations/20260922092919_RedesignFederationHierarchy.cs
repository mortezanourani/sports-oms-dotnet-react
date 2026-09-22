using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RedesignFederationHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "sport_type",
                table: "federations",
                newName: "level");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "federations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "federations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "federations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_federation_id",
                table: "federations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "province_id",
                table: "federations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_federations_city_id",
                table: "federations",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_federations_parent_federation_id",
                table: "federations",
                column: "parent_federation_id");

            migrationBuilder.CreateIndex(
                name: "ix_federations_province_id",
                table: "federations",
                column: "province_id");

            migrationBuilder.AddForeignKey(
                name: "fk_federations_cities_city_id",
                table: "federations",
                column: "city_id",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_federations_federations_parent_federation_id",
                table: "federations",
                column: "parent_federation_id",
                principalTable: "federations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_federations_provinces_province_id",
                table: "federations",
                column: "province_id",
                principalTable: "provinces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_federations_cities_city_id",
                table: "federations");

            migrationBuilder.DropForeignKey(
                name: "fk_federations_federations_parent_federation_id",
                table: "federations");

            migrationBuilder.DropForeignKey(
                name: "fk_federations_provinces_province_id",
                table: "federations");

            migrationBuilder.DropIndex(
                name: "ix_federations_city_id",
                table: "federations");

            migrationBuilder.DropIndex(
                name: "ix_federations_parent_federation_id",
                table: "federations");

            migrationBuilder.DropIndex(
                name: "ix_federations_province_id",
                table: "federations");

            migrationBuilder.DropColumn(
                name: "category",
                table: "federations");

            migrationBuilder.DropColumn(
                name: "city_id",
                table: "federations");

            migrationBuilder.DropColumn(
                name: "parent_federation_id",
                table: "federations");

            migrationBuilder.DropColumn(
                name: "province_id",
                table: "federations");

            migrationBuilder.RenameColumn(
                name: "level",
                table: "federations",
                newName: "sport_type");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "federations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
