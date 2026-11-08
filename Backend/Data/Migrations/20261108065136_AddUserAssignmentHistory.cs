using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_assignment_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    scope_level = table.Column<string>(type: "text", nullable: false),
                    scope_province_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_legacy = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_assignment_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_assignment_histories_cities_scope_city_id",
                        column: x => x.scope_city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_assignment_histories_provinces_scope_province_id",
                        column: x => x.scope_province_id,
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_assignment_histories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_histories_scope_city_id",
                table: "user_assignment_histories",
                column: "scope_city_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_histories_scope_province_id",
                table: "user_assignment_histories",
                column: "scope_province_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_histories_user_id",
                table: "user_assignment_histories",
                column: "user_id",
                unique: true,
                filter: "\"ended_at_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_histories_user_id_started_at_utc",
                table: "user_assignment_histories",
                columns: new[] { "user_id", "started_at_utc" });

            migrationBuilder.Sql("""
                INSERT INTO user_assignment_histories
                    (id, user_id, role, scope_level, scope_province_id, scope_city_id,
                     started_at_utc, ended_at_utc, is_legacy, changed_by_user_id)
                SELECT gen_random_uuid(), id, role, scope_level, scope_province_id, scope_city_id,
                       NULL, NULL, TRUE, NULL
                FROM "AspNetUsers"
                WHERE role <> 'FederationRep';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_assignment_histories");
        }
    }
}
