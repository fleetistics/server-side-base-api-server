using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260925143654 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. Scaffolded mid-way through moving the notification model into
            // server-side-notifications, it re-added four FKs (fcm_notification_sent, notification_queue,
            // notification_sent, notification2user) that the database already has - the snapshot it
            // was diffed against had momentarily lost them. Nothing to change in the schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty to match Up(): those FKs predate this migration, so rolling it back must not drop them.
        }
    }
}
