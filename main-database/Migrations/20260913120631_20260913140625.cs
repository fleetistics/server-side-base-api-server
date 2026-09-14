using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260913140625 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_user_location_privacy_user_UserId",
                table: "user_location_privacy",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_location_privacy_user_UserId",
                table: "user_location_privacy");
        }
    }
}
