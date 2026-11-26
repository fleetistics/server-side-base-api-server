using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261123225941 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "user_message",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "MobileGpsDeviceId",
                table: "user_emergency_alert",
                newName: "GpsDeviceId");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "user_emergency_alert",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "user_emergency_alert",
                type: "geometry",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "user_message",
                newName: "TeamId");

            migrationBuilder.RenameColumn(
                name: "GpsDeviceId",
                table: "user_emergency_alert",
                newName: "MobileGpsDeviceId");

            migrationBuilder.AddColumn<float>(
                name: "CompleteLatitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CompleteLongitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Latitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Longitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);
        }
    }
}
