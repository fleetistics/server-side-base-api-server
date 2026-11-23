using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261123172743 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Token_LocationReportTokenLatestRefreshTime",
                table: "user_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Token_LocationReportTokenRotationCounter",
                table: "user_session",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Token_LocationReportTokenLatestRefreshTime",
                table: "user_session");

            migrationBuilder.DropColumn(
                name: "Token_LocationReportTokenRotationCounter",
                table: "user_session");
        }
    }
}
