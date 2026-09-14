using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260912112434 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MobileGpsDeviceId",
                table: "user_emergency_alert",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
