using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAndGpsDeviceMapState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "gps_device",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gps_device_map_state",
                columns: table => new
                {
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    Dir = table.Column<short>(type: "smallint", nullable: true),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPositionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCommunicationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LowBattery = table.Column<bool>(type: "boolean", nullable: false),
                    EngineFault = table.Column<bool>(type: "boolean", nullable: false),
                    LatestOnMapUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_map_state", x => x.GpsDeviceId);
                })
                .Annotation("Npgsql:StorageParameter:fillfactor", 75);

            migrationBuilder.CreateTable(
                name: "gps_device_map_status",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_map_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gps_device_state_data",
                columns: table => new
                {
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    LatestOnMapUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_state_data", x => x.GpsDeviceId);
                })
                .Annotation("Npgsql:StorageParameter:fillfactor", 75);

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_CompanyId",
                table: "gps_device",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_map_state_CompanyId",
                table: "gps_device_map_state",
                column: "CompanyId");

            // Reference rows (this repo creates ref tables empty). Idempotent: a row inserted by hand before is kept.
            migrationBuilder.Sql(
                "INSERT INTO gps_device_map_status (\"Id\", \"Name\") VALUES (0, 'Unknown'), (1, 'Moving'), (2, 'Idle'), (3, 'Parked'), (4, 'Offline') ON CONFLICT (\"Id\") DO NOTHING;");

            migrationBuilder.Sql(
                "INSERT INTO gps_device_provider (\"Id\", \"Name\") VALUES (3, 'Geotab') ON CONFLICT (\"Id\") DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_gps_device_CompanyId",
                table: "gps_device");
        }
    }
}
