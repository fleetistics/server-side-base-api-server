using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261116192816 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_DeviceUID",
                table: "user_session",
                newName: "ClientInfo_PlatformDescription");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_CodeVersion",
                table: "user_session",
                newName: "ClientInfo_AppHash");

            migrationBuilder.AlterColumn<short>(
                name: "ClientInfo_ClientApplicationId",
                table: "user_session",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_PlatformDescription",
                table: "user_session",
                newName: "ClientInfo_DeviceUID");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_AppHash",
                table: "user_session",
                newName: "ClientInfo_CodeVersion");

            migrationBuilder.AlterColumn<int>(
                name: "ClientInfo_ClientApplicationId",
                table: "user_session",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<string>(
                name: "ClientInfo_AppUID",
                table: "user_session",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
