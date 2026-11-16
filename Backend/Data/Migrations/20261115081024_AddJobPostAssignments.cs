using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    placement_scope = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_posts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_post_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_post_name = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_post_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_post_assignments_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_post_assignments_job_posts_job_post_id",
                        column: x => x.job_post_id,
                        principalTable: "job_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_post_assignments_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_job_post_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_post_assignments_city_id",
                table: "job_post_assignments",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_post_assignments_job_post_id",
                table: "job_post_assignments",
                column: "job_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_post_assignments_province_id",
                table: "job_post_assignments",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_post_assignments_user_id",
                table: "job_post_assignments",
                column: "user_id",
                unique: true,
                filter: "\"ended_on\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_job_post_assignments_user_id_started_on",
                table: "job_post_assignments",
                columns: new[] { "user_id", "started_on" });

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_name",
                table: "job_posts",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_post_assignments");

            migrationBuilder.DropTable(
                name: "job_posts");
        }
    }
}
