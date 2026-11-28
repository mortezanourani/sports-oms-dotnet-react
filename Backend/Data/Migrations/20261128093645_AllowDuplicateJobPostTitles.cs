using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateJobPostTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_job_posts_title",
                table: "job_posts");

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_title",
                table: "job_posts",
                column: "title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_job_posts_title",
                table: "job_posts");

            migrationBuilder.CreateIndex(
                name: "ix_job_posts_title",
                table: "job_posts",
                column: "title",
                unique: true);
        }
    }
}
