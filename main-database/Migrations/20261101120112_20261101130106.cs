using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261101130106 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationDeviceDate",
                table: "gps_device_state_data",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationServerDate",
                table: "gps_device_state_data",
                type: "timestamp with time zone",
                nullable: true);

            // LastLocationServerDate is the database's own clock at the moment LastLocationDeviceDate was set or changed:
            // on insert only when a device date is given, on update only when the device date differs. No application
            // (the API, the feeders' raw upserts) writes it.
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
