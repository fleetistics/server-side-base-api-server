using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class NotificationTransportTypeAndOptionalOutboxIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. The scaffolder emitted notification."TransportType" (smallint NOT NULL DEFAULT 1) and
            // dropped NOT NULL from notification_outbox."NotificationId" and ."UserId", but those tables are owned
            // by server-side-notifications (NotificationDatabaseContext, history table
            // __EFMigrationsHistory_Notifications, migration 20261023133705_AddNotificationTransportType).
            // The change is applied by hand with its idempotent script, which must be run before this version is
            // deployed: server-side-notifications/exs.notifications-database/Scripts/20261023133705_AddNotificationTransportType.sql
            // The Designer/snapshot still record the full model, so EF sees no pending model changes.
            // See 20261017133027_20261017163023 for the same arrangement.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
