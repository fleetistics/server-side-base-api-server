using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261003163126 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_team_details_team_TeamId",
                table: "team_details");

            migrationBuilder.DropForeignKey(
                name: "FK_team_uploaded_media_team_TeamId1",
                table: "team_uploaded_media");

            migrationBuilder.DropIndex(
                name: "IX_team_uploaded_media_TeamId1",
                table: "team_uploaded_media");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId1",
                table: "team_uploaded_media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_team_uploaded_media_TeamId1",
                table: "team_uploaded_media",
                column: "TeamId1");

            migrationBuilder.AddForeignKey(
                name: "FK_team_details_team_TeamId",
                table: "team_details",
                column: "TeamId",
                principalTable: "team",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_team_uploaded_media_team_TeamId1",
                table: "team_uploaded_media",
                column: "TeamId1",
                principalTable: "team",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
