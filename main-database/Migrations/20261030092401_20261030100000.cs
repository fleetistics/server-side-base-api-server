using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261030100000 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gps_device_location_privacy",
                columns: table => new
                {
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    PrivacyMode = table.Column<short>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_location_privacy", x => x.GpsDeviceId);
                    table.ForeignKey(
                        name: "FK_gps_device_location_privacy_gps_device_GpsDeviceId",
                        column: x => x.GpsDeviceId,
                        principalTable: "gps_device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "gps_device_location_privacy");
        }
    }
}
