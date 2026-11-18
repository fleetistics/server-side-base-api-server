using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261116205139 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionKey",
                table: "user_session",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_session_SessionKey",
                table: "user_session",
                column: "SessionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_session_SessionKey",
                table: "user_session");
        }
    }
}
