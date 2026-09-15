using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestrictFederationDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_athlete_counts_federations_federation_id",
                table: "athlete_counts");

            migrationBuilder.DropForeignKey(
                name: "fk_federation_positions_federations_federation_id",
                table: "federation_positions");

            migrationBuilder.AddForeignKey(
                name: "fk_athlete_counts_federations_federation_id",
                table: "athlete_counts",
                column: "federation_id",
                principalTable: "federations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_federation_positions_federations_federation_id",
                table: "federation_positions",
                column: "federation_id",
                principalTable: "federations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_athlete_counts_federations_federation_id",
                table: "athlete_counts");

            migrationBuilder.DropForeignKey(
                name: "fk_federation_positions_federations_federation_id",
                table: "federation_positions");

            migrationBuilder.AddForeignKey(
                name: "fk_athlete_counts_federations_federation_id",
                table: "athlete_counts",
                column: "federation_id",
                principalTable: "federations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_federation_positions_federations_federation_id",
                table: "federation_positions",
                column: "federation_id",
                principalTable: "federations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
