using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261101131922 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing read it: LatestUpdate (stamped by the database) says when the row changed. The model differ never scripts a
            // drop, so it is written by hand.
            migrationBuilder.DropColumn(
                name: "LatestOnMapUpdate",
                table: "gps_device_state_data");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The values are gone; existing rows get the time of the rollback.
            migrationBuilder.AddColumn<DateTime>(
                name: "LatestOnMapUpdate",
                table: "gps_device_state_data",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }
    }
}
