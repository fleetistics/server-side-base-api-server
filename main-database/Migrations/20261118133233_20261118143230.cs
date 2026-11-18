using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261118143230 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_PlatformDescription",
                table: "user_session",
                newName: "ClientInfo_PlatformDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_PlatformDetails",
                table: "user_session",
                newName: "ClientInfo_PlatformDescription");
        }
    }
}
