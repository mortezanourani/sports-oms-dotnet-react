using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    public partial class AddOrgUnitProvinceAndCity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "province_id",
                table: "org_units",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "org_units",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE org_units
                SET province_id = (SELECT id FROM provinces WHERE name = 'گیلان');

                UPDATE org_units
                SET city_id = (
                    SELECT cities.id
                    FROM cities
                    JOIN provinces ON provinces.id = cities.province_id
                    WHERE cities.name = 'رشت' AND provinces.name = 'گیلان'
                )
                WHERE scope = 'City';

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM org_units WHERE province_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot backfill OrgUnit ProvinceId: province گیلان must exist.';
                    END IF;
                    IF EXISTS (
                        SELECT 1
                        FROM org_units
                        WHERE (scope = 'Provincial' AND city_id IS NOT NULL)
                           OR (scope = 'City' AND city_id IS NULL)
                    ) THEN
                        RAISE EXCEPTION 'Cannot backfill OrgUnit CityId: city رشت in province گیلان must exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "province_id",
                table: "org_units",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_units_city_id",
                table: "org_units",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_province_id",
                table: "org_units",
                column: "province_id");

            migrationBuilder.AddForeignKey(
                name: "fk_org_units_cities_city_id",
                table: "org_units",
                column: "city_id",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_units_provinces_province_id",
                table: "org_units",
                column: "province_id",
                principalTable: "provinces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_units_cities_city_id",
                table: "org_units");

            migrationBuilder.DropForeignKey(
                name: "fk_org_units_provinces_province_id",
                table: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_org_units_city_id",
                table: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_org_units_province_id",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "city_id",
                table: "org_units");

            migrationBuilder.DropColumn(
                name: "province_id",
                table: "org_units");
        }
    }
}
