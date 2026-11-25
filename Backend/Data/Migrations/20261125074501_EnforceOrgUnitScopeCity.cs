using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOrgUnitScopeCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_org_units_scope_city",
                table: "org_units",
                sql: "(scope = 'Provincial' AND city_id IS NULL) OR (scope = 'City' AND city_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_org_units_scope_city",
                table: "org_units");
        }
    }
}
