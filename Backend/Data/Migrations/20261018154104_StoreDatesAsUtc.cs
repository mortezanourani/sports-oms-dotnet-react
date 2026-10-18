using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreDatesAsUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "federation_positions"
                    ALTER COLUMN "start_date" TYPE timestamp with time zone
                        USING ("start_date"::timestamp AT TIME ZONE 'Asia/Tehran'),
                    ALTER COLUMN "end_date" TYPE timestamp with time zone
                        USING ("end_date"::timestamp AT TIME ZONE 'Asia/Tehran');

                ALTER TABLE "facility_licenses"
                    ALTER COLUMN "issue_date" TYPE timestamp with time zone
                        USING ("issue_date"::timestamp AT TIME ZONE 'Asia/Tehran'),
                    ALTER COLUMN "expiry_date" TYPE timestamp with time zone
                        USING ("expiry_date"::timestamp AT TIME ZONE 'Asia/Tehran');

                ALTER TABLE "facility_contracts"
                    ALTER COLUMN "start_date" TYPE timestamp with time zone
                        USING ("start_date"::timestamp AT TIME ZONE 'Asia/Tehran'),
                    ALTER COLUMN "end_date" TYPE timestamp with time zone
                        USING ("end_date"::timestamp AT TIME ZONE 'Asia/Tehran');

                ALTER TABLE "champions"
                    ALTER COLUMN "birth_date" TYPE timestamp with time zone
                        USING ("birth_date"::timestamp AT TIME ZONE 'Asia/Tehran');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "federation_positions"
                    ALTER COLUMN "start_date" TYPE date
                        USING (("start_date" AT TIME ZONE 'Asia/Tehran')::date),
                    ALTER COLUMN "end_date" TYPE date
                        USING (("end_date" AT TIME ZONE 'Asia/Tehran')::date);

                ALTER TABLE "facility_licenses"
                    ALTER COLUMN "issue_date" TYPE date
                        USING (("issue_date" AT TIME ZONE 'Asia/Tehran')::date),
                    ALTER COLUMN "expiry_date" TYPE date
                        USING (("expiry_date" AT TIME ZONE 'Asia/Tehran')::date);

                ALTER TABLE "facility_contracts"
                    ALTER COLUMN "start_date" TYPE date
                        USING (("start_date" AT TIME ZONE 'Asia/Tehran')::date),
                    ALTER COLUMN "end_date" TYPE date
                        USING (("end_date" AT TIME ZONE 'Asia/Tehran')::date);

                ALTER TABLE "champions"
                    ALTER COLUMN "birth_date" TYPE date
                        USING (("birth_date" AT TIME ZONE 'Asia/Tehran')::date);
                """);
        }
    }
}
