using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261017163023 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. MainDatabaseContext maps the notification tables via NotificationsModelBuilder
            // (server-side-notifications), so the scaffolder emitted CreateTable/CreateIndex for the sender-mode
            // outbox. None of that belongs here: notification_outbox (with UX_notification_outbox_MessageId,
            // IX_notification_outbox_pending, IX_notification_outbox_NotificationId_UserId,
            // IX_notification_outbox_CompletedAt, the FK to notification) and notification_transport_cursor are
            // owned by server-side-notifications' migration 20261017104840_AddNotificationOutbox
            // (NotificationDatabaseContext, history table __EFMigrationsHistory_Notifications).
            // The Designer/snapshot still record the full model, so EF sees no pending model changes.
            // Strip future scaffolded migrations of notifications-owned objects the same way.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
