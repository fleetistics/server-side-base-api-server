using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class RecreateGpsDeviceMapStateAndStateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both tables are dropped and created again from the current model, so every database ends up with exactly the
            // model's columns: no leftover columns from the earlier "columns stay for one release" steps. Existing rows are
            // lost; both tables are rebuilt by the feeders. The DROPs are hand-written (the model differ never scripts one)
            // and IF EXISTS, so a database where a table was already dropped by hand still migrates.
            migrationBuilder.Sql("DROP TABLE IF EXISTS gps_device_map_state;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS gps_device_state_data;");

            migrationBuilder.CreateTable(
                name: "gps_device_map_state",
                columns: table => new
                {
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    Dir = table.Column<short>(type: "smallint", nullable: true),
                    StatusId = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ActivityStatusId = table.Column<short>(type: "smallint", nullable: false),
                    WarningFlags = table.Column<short>(type: "smallint", nullable: false),
                    LocationAccuracyId = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_map_state", x => x.GpsDeviceId);
                })
                .Annotation("Npgsql:StorageParameter:fillfactor", 75);

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_map_state_CompanyId",
                table: "gps_device_map_state",
                column: "CompanyId");

            migrationBuilder.CreateTable(
                name: "gps_device_state_data",
                columns: table => new
                {
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    LastCommunicationDeviceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLocationDeviceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstLocationDeviceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_state_data", x => x.GpsDeviceId);
                })
                .Annotation("Npgsql:StorageParameter:fillfactor", 75);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
