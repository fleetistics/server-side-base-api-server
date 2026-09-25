using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260917222404 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_FCMToken",
                table: "user_session",
                newName: "ClientInfo_FCM_FID");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_ClientDevicePlatformId",
                table: "user_session",
                newName: "ClientInfo_PlatformId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_PlatformId",
                table: "user_session",
                newName: "ClientInfo_ClientDevicePlatformId");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_FCM_FID",
                table: "user_session",
                newName: "ClientInfo_FCMToken");
        }
    }
}
