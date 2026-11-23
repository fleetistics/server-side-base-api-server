using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <summary>
    /// Intentionally empty. As generated (on a branch whose model predated the user_session refresh-key changes) it
    /// renamed SessionKey / ClientInfo_PlatformDetails / ClientInfo_AppHash back to ClientInfo_DeviceUID /
    /// ClientInfo_CodeVersion / ClientInfo_AppUID and widened client_application.Id to integer, and the next migration
    /// (20261118150918) renamed them forward again. The pair cancels out, so both are no-ops: a fresh database goes
    /// straight to the current model, and an existing one is not touched. The shared dev database had already run
    /// this migration and was repaired by hand (main-database/custom-sql/one-time-dev-db-repair-20261123.sql).
    /// </summary>
    public partial class _20261118170351 : Migration
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
