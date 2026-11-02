using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261102110000 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The model differ never scripts a drop, so this is written by hand. The triggers go first: they assign the column.
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_gps_device_state_data_location_server_date_insert ON ""gps_device_state_data"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_gps_device_state_data_location_server_date_update ON ""gps_device_state_data"";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS set_last_location_server_date();");

            migrationBuilder.DropColumn(
                name: "LastLocationServerDate",
                table: "gps_device_state_data");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The values are gone; rows get no server date until their LastLocationDeviceDate changes.
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationServerDate",
                table: "gps_device_state_data",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION set_last_location_server_date()
                RETURNS trigger AS $$
                BEGIN
                    NEW."LastLocationServerDate" = clock_timestamp();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                CREATE OR REPLACE TRIGGER trg_gps_device_state_data_location_server_date_insert
                BEFORE INSERT ON "gps_device_state_data"
                FOR EACH ROW WHEN (NEW."LastLocationDeviceDate" IS NOT NULL)
                EXECUTE FUNCTION set_last_location_server_date();
                """);
            migrationBuilder.Sql("""
                CREATE OR REPLACE TRIGGER trg_gps_device_state_data_location_server_date_update
                BEFORE UPDATE ON "gps_device_state_data"
                FOR EACH ROW WHEN (NEW."LastLocationDeviceDate" IS DISTINCT FROM OLD."LastLocationDeviceDate")
                EXECUTE FUNCTION set_last_location_server_date();
                """);
        }
    }
}
