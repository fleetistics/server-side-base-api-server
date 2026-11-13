using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <summary>
    /// Refresh-cookie hardening: the cookie is now "{sessionId}.{counter}.{mac}" derived on the server, so
    /// the two plaintext rotation-key columns go away and a per-session counter and salt replace them.
    /// Written by hand (the generated version renamed Token_SessionRotationKey to Token_KeySalt, which would
    /// have turned old replayable keys into salts, and the migration differ never emits drops).
    /// </summary>
    public partial class _20261113093814 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Token_RotationCounter",
                table: "user_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Token_KeySalt",
                table: "user_session",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Portal / app logins (sources 2 and 3) hold cookies in the old format and cannot be migrated:
            // everyone signs in once after the deploy. Auto-device sessions (source 1) never use the cookie
            // and stay Active. Status 3 = UserSessionStatus.Expired.
            migrationBuilder.Sql("""
                UPDATE user_session
                SET "StatusId" = 3
                WHERE "StatusId" = 1 AND "SessionSourceId" IN (2, 3);
                """);

            migrationBuilder.DropColumn(
                name: "Token_SessionRotationKey",
                table: "user_session");

            migrationBuilder.DropColumn(
                name: "Token_PreviousSessionRotationKey",
                table: "user_session");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old keys cannot be restored (they were never recoverable from the new data); the columns
            // come back empty, so sessions of the old format are gone either way.
            migrationBuilder.AddColumn<string>(
                name: "Token_SessionRotationKey",
                table: "user_session",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Token_PreviousSessionRotationKey",
                table: "user_session",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropColumn(
                name: "Token_KeySalt",
                table: "user_session");

            migrationBuilder.DropColumn(
                name: "Token_RotationCounter",
                table: "user_session");
        }
    }
}
