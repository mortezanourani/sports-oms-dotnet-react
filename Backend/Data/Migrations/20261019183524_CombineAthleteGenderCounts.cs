using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class CombineAthleteGenderCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_athlete_counts_federation_id_city_id_year_month_gender",
                table: "athlete_counts");

            migrationBuilder.AddColumn<int>(
                name: "female_count",
                table: "athlete_counts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "male_count",
                table: "athlete_counts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE athlete_counts
                SET male_count = count
                WHERE gender = 'Male';

                UPDATE athlete_counts
                SET female_count = count
                WHERE gender = 'Female';

                DELETE FROM athlete_counts AS female
                USING athlete_counts AS male
                WHERE female.gender = 'Female'
                  AND male.gender = 'Male'
                  AND female.federation_id = male.federation_id
                  AND female.city_id = male.city_id
                  AND female.year = male.year
                  AND female.month = male.month;
                """);

            migrationBuilder.DropColumn(
                name: "count",
                table: "athlete_counts");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "athlete_counts");

            migrationBuilder.CreateIndex(
                name: "ix_athlete_counts_federation_id_city_id_year_month",
                table: "athlete_counts",
                columns: new[] { "federation_id", "city_id", "year", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_athlete_counts_federation_id_city_id_year_month",
                table: "athlete_counts");

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "athlete_counts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "count",
                table: "athlete_counts",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO athlete_counts
                    (id, federation_id, city_id, year, month, gender, count)
                SELECT gen_random_uuid(), federation_id, city_id, year, month, 'Female', female_count
                FROM athlete_counts;

                UPDATE athlete_counts
                SET gender = 'Male', count = male_count;
                """);

            migrationBuilder.DropColumn(
                name: "female_count",
                table: "athlete_counts");

            migrationBuilder.DropColumn(
                name: "male_count",
                table: "athlete_counts");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "athlete_counts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "count",
                table: "athlete_counts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_athlete_counts_federation_id_city_id_year_month_gender",
                table: "athlete_counts",
                columns: new[] { "federation_id", "city_id", "year", "month", "gender" },
                unique: true);
        }
    }
}
