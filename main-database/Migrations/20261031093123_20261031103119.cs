using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261031103119 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "mobile_gps_device_track_point",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "MobileGpsDeviceId",
                table: "mobile_gps_device_track_point",
                newName: "GpsDeviceId");

            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "mobile_gps_device_track_point",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GpsDeviceId",
                table: "mobile_gps_device_track_point",
                newName: "MobileGpsDeviceId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "mobile_gps_device_track_point",
                newName: "TeamId");

            migrationBuilder.AddColumn<bool>(
                name: "BatteryIsCharging",
                table: "mobile_gps_device_track_point",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "BatteryLevel",
                table: "mobile_gps_device_track_point",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Odometer",
                table: "mobile_gps_device_track_point",
                type: "real",
                nullable: true);
        }
    }
}
