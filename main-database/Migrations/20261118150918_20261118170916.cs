using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <summary>
    /// Intentionally empty: the counterpart of 20261118150355, see there. As generated it renamed the columns forward
    /// again and re-added ClientInfo_ClientApplicationId, CompanyId and client_application.Uid, which already exist
    /// (ClientInfo_ClientApplicationId since 20261115232617), so it failed on every database: "column already exists".
    /// </summary>
    public partial class _20261118170916 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
