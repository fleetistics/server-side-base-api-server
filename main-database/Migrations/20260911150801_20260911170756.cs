using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260911170756 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_user_GovIDImageId",
                table: "user",
                column: "GovIDImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_uploaded_media_GovIDImageId",
                table: "user",
                column: "GovIDImageId",
                principalTable: "uploaded_media",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_uploaded_media_GovIDImageId",
                table: "user");

            migrationBuilder.DropIndex(
                name: "IX_user_GovIDImageId",
                table: "user");
        }
    }
}
