using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM job_posts) THEN
                        RAISE EXCEPTION 'Cannot add required JobPost identifiers: job_posts must be empty.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "job_post_code",
                table: "job_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "job_post_unique_id",
                table: "job_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "job_post_code",
                table: "job_posts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "job_post_unique_id",
                table: "job_posts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_job_post_code",
                table: "job_posts",
                column: "job_post_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_job_post_unique_id",
                table: "job_posts",
                column: "job_post_unique_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_job_posts_job_post_code",
                table: "job_posts");

            migrationBuilder.DropIndex(
                name: "ix_job_posts_job_post_unique_id",
                table: "job_posts");

            migrationBuilder.DropColumn(
                name: "job_post_code",
                table: "job_posts");

            migrationBuilder.DropColumn(
                name: "job_post_unique_id",
                table: "job_posts");
        }
    }
}
