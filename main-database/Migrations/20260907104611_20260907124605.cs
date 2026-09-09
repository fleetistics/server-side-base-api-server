using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260907124605 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_session_gps_device_GpsDeviceId",
                table: "user_session");

            migrationBuilder.RenameColumn(
                name: "GpsDeviceId",
                table: "user_session",
                newName: "MobileGpsDeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_user_session_GpsDeviceId",
                table: "user_session",
                newName: "IX_user_session_MobileGpsDeviceId");

            migrationBuilder.CreateTable(
                name: "gps_location_accuracy",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_location_accuracy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_device_motion_activity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_device_motion_activity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_gps_device",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    DeviceUID = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()"),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_gps_device", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_gps_device_map_state",
                columns: table => new
                {
                    MobileGpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    MotionActivity = table.Column<string>(type: "text", nullable: true),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    Dir = table.Column<short>(type: "smallint", nullable: true),
                    BatteryLevel = table.Column<float>(type: "real", nullable: true),
                    LatestOnMapUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_gps_device_map_state", x => x.MobileGpsDeviceId);
                });

            migrationBuilder.CreateTable(
                name: "mobile_gps_device_provider",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_gps_device_provider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_gps_device_track_point",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MobileGpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    Odometer = table.Column<float>(type: "real", nullable: true),
                    Dir = table.Column<short>(type: "smallint", nullable: true),
                    LocationAccuracy = table.Column<short>(type: "smallint", nullable: false),
                    BatteryLevel = table.Column<float>(type: "real", nullable: true),
                    BatteryIsCharging = table.Column<bool>(type: "boolean", nullable: true),
                    MotionActivity = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_gps_device_track_point", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_user_session_mobile_gps_device_MobileGpsDeviceId",
                table: "user_session",
                column: "MobileGpsDeviceId",
                principalTable: "mobile_gps_device",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_session_mobile_gps_device_MobileGpsDeviceId",
                table: "user_session");

            migrationBuilder.RenameColumn(
                name: "MobileGpsDeviceId",
                table: "user_session",
                newName: "GpsDeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_user_session_MobileGpsDeviceId",
                table: "user_session",
                newName: "IX_user_session_GpsDeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_session_gps_device_GpsDeviceId",
                table: "user_session",
                column: "GpsDeviceId",
                principalTable: "gps_device",
                principalColumn: "Id");
        }
    }
}
