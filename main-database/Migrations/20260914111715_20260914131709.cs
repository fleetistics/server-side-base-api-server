using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260914131709 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_team_member_user_team_TeamId1",
                table: "team_member_user");

            migrationBuilder.DropIndex(
                name: "IX_team_member_user_TeamId1",
                table: "team_member_user");

            migrationBuilder.DropColumn(
                name: "TeamId1",
                table: "team_member_user");

            migrationBuilder.AddColumn<short>(
                name: "IconKey",
                table: "team_member_user",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId1",
                table: "team_member_user",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_member_user_TeamId1",
                table: "team_member_user",
                column: "TeamId1");

            migrationBuilder.AddForeignKey(
                name: "FK_team_member_user_team_TeamId1",
                table: "team_member_user",
                column: "TeamId1",
                principalTable: "team",
                principalColumn: "Id");
        }
    }
}
