using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261101134524 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MotionActivity",
                table: "mobile_gps_device_track_point",
                newName: "ActivityStatusId");

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestUpdate",
                table: "mobile_gps_device_track_point",
                type: "timestamp with time zone",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "now()");

            // ReceivedDate is no longer mapped. The column stays for one release (this repo never drops in a migration) but must
            // not block an insert that does not know it.
            migrationBuilder.Sql(@"ALTER TABLE mobile_gps_device_track_point ALTER COLUMN ""ReceivedDate"" DROP NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE mobile_gps_device_track_point SET ""ReceivedDate"" = COALESCE(""ReceivedDate"", ""DeviceDate"");
ALTER TABLE mobile_gps_device_track_point ALTER COLUMN ""ReceivedDate"" SET NOT NULL;");

            migrationBuilder.DropColumn(
                name: "LatestUpdate",
                table: "mobile_gps_device_track_point");

            migrationBuilder.RenameColumn(
                name: "ActivityStatusId",
                table: "mobile_gps_device_track_point",
                newName: "MotionActivity");
        }
    }
}
