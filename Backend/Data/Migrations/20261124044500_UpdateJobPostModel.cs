using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261124044500_UpdateJobPostModel")]
    public partial class UpdateJobPostModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "job_post_assignments");

            migrationBuilder.DropIndex(
                name: "ix_job_posts_name",
                table: "job_posts");

            migrationBuilder.DropColumn(name: "is_active", table: "job_posts");
            migrationBuilder.DropColumn(name: "name", table: "job_posts");
            migrationBuilder.DropColumn(name: "placement_scope", table: "job_posts");

            migrationBuilder.AddColumn<bool>(
                name: "break_out",
                table: "job_posts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manager_type",
                table: "job_posts",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "org_unit_id",
                table: "job_posts",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "job_posts",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "type_code",
                table: "job_posts",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "job_post_type_id",
                table: "job_posts",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_job_post_type_id",
                table: "job_posts",
                column: "job_post_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_org_unit_id",
                table: "job_posts",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_title",
                table: "job_posts",
                column: "title",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_job_posts_job_post_types_job_post_type_id",
                table: "job_posts",
                column: "job_post_type_id",
                principalTable: "job_post_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_job_posts_org_units_org_unit_id",
                table: "job_posts",
                column: "org_unit_id",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_job_posts_job_post_types_job_post_type_id",
                table: "job_posts");

            migrationBuilder.DropForeignKey(
                name: "fk_job_posts_org_units_org_unit_id",
                table: "job_posts");

            migrationBuilder.DropIndex(name: "ix_job_posts_job_post_type_id", table: "job_posts");
            migrationBuilder.DropIndex(name: "ix_job_posts_org_unit_id", table: "job_posts");
            migrationBuilder.DropIndex(name: "ix_job_posts_title", table: "job_posts");

            migrationBuilder.DropColumn(name: "break_out", table: "job_posts");
            migrationBuilder.DropColumn(name: "manager_type", table: "job_posts");
            migrationBuilder.DropColumn(name: "org_unit_id", table: "job_posts");
            migrationBuilder.DropColumn(name: "type_code", table: "job_posts");
            migrationBuilder.DropColumn(name: "job_post_type_id", table: "job_posts");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "job_posts",
                newName: "name");

            migrationBuilder.RenameIndex(
                name: "ix_job_posts_title",
                table: "job_posts",
                newName: "ix_job_posts_name");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "job_posts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "placement_scope",
                table: "job_posts",
                type: "text",
                nullable: false,
                defaultValue: "Both");

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
        }
    }
}
