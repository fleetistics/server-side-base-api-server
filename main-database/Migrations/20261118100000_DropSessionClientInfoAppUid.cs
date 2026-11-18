using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class DropSessionClientInfoAppUid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 20261116192816 renamed the other ClientInfo_* columns but never dropped ClientInfo_AppUID (its Down()
            // re-adds it). SessionClientInfo no longer has AppUID, so inserts into user_session failed with
            // 23502 (NOT NULL). IF EXISTS keeps this safe on databases where the column is already gone.
            migrationBuilder.Sql("ALTER TABLE user_session DROP COLUMN IF EXISTS \"ClientInfo_AppUID\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientInfo_AppUID",
                table: "user_session",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
