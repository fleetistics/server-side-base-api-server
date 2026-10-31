using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261029164524 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gps_device_hourly_total",
                columns: table => new
                {
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    HourUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Odometer = table.Column<double>(type: "double precision", nullable: true),
                    OdometerAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EngineHours = table.Column<double>(type: "double precision", nullable: true),
                    EngineHoursAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FuelTotal = table.Column<double>(type: "double precision", nullable: true),
                    FuelTotalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FuelIdleTotal = table.Column<double>(type: "double precision", nullable: true),
                    FuelIdleTotalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_hourly_total", x => new { x.GpsDeviceId, x.HourUtc });
                    table.ForeignKey(
                        name: "FK_gps_device_hourly_total_gps_device_GpsDeviceId",
                        column: x => x.GpsDeviceId,
                        principalTable: "gps_device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_hourly_total_HourUtc",
                table: "gps_device_hourly_total",
                column: "HourUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gps_device_hourly_total");
        }
    }
}
