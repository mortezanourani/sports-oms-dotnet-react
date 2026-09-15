using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAthleteCountUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_athlete_counts_federation_id",
                table: "athlete_counts");

            migrationBuilder.CreateIndex(
                name: "ix_athlete_counts_federation_id_city_id_year_month_gender",
                table: "athlete_counts",
                columns: new[] { "federation_id", "city_id", "year", "month", "gender" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_athlete_counts_federation_id_city_id_year_month_gender",
                table: "athlete_counts");

            migrationBuilder.CreateIndex(
                name: "ix_athlete_counts_federation_id",
                table: "athlete_counts",
                column: "federation_id");
        }
    }
}
