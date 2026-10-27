using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261023212500 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_user_password_reset_key",
                table: "user_password_reset_key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_password_reset_key",
                table: "user_password_reset_key",
                column: "KeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_user_password_reset_key_UserId",
                table: "user_password_reset_key",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_user_password_reset_key",
                table: "user_password_reset_key");

            migrationBuilder.DropIndex(
                name: "IX_user_password_reset_key_UserId",
                table: "user_password_reset_key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_password_reset_key",
                table: "user_password_reset_key",
                column: "UserId");
        }
    }
}
