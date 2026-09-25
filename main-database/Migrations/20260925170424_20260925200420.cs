using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260925200420 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. MainDatabaseContext now maps the notification tables via
            // NotificationsModelBuilder (server-side-notifications), so the scaffolder emitted CreateTable
            // for all of them. None of that belongs here:
            //  - notification, notification_type, notification2user are owned by this repo's migrations
            //    and already exist (with IX_notification2user_NotificationId and the LatestUpdate trigger).
            //  - fcm_queue, fcm_sent, notification."Status" and their indexes (IX_fcm_queue_Date,
            //    IX_fcm_queue_NotificationId, IX_fcm_sent_NotificationId_UserId, IX_notification_unprocessed)
            //    are owned by server-side-notifications' own migrations (NotificationDatabaseContext,
            //    history table __EFMigrationsHistory_Notifications) - see the table-ownership note in
            //    NotificationsModelBuilder.cs.
            // The Designer/snapshot still record the full model, so EF sees no pending model changes.
            // Strip future scaffolded migrations of notifications-owned objects the same way.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
