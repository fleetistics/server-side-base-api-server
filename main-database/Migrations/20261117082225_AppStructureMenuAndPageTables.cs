using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class AppStructureMenuAndPageTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. The scaffolder emitted CreateTable for application_menu, application_menu_item and
            // application_page, but those tables already exist (they were created by hand), so Up must not create them.
            // The Designer/snapshot still record the full model, so EF sees no pending model changes.
            // A fresh database must create the tables by hand with main-database/custom-sql/app-structure-tables.sql
            // (derived from the scaffolded migration; the column types of the existing tables were not verified against it).
            // See 20261111141855_NotificationTemplateTable for the same arrangement.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
