using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class NotificationTemplateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. The scaffolder emitted the notification_template table and its two unique indexes, but
            // that table is owned by server-side-notifications (NotificationDatabaseContext, history table
            // __EFMigrationsHistory_Notifications, migration 20261111132501_AddNotificationTemplate).
            // It is created by hand with its idempotent script, which must be run (and the application login granted
            // rights on the table, see the end of the script) before this version is deployed:
            // server-side-notifications/exs.notifications-database/Scripts/20261111132501_AddNotificationTemplate.sql
            // The Designer/snapshot still record the full model, so EF sees no pending model changes.
            // See 20261023145318_NotificationTransportTypeAndOptionalOutboxIds for the same arrangement.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
