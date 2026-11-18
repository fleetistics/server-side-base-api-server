using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261116134952 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. The scaffolder emitted the notification_template table and its two unique indexes, but
            // that table is owned by server-side-notifications (NotificationDatabaseContext) and is created by hand
            // with its idempotent script (see 20261111141855_NotificationTemplateTable for the same arrangement).
            // The Designer/snapshot still record the full model, so EF sees no pending model changes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
