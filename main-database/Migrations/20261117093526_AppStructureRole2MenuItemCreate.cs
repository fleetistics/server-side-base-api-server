using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class AppStructureRole2MenuItemCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The role2menu_item table did not exist when the earlier migration 20261117092433_AppStructureRole2MenuItemTable
            // was applied (its Up was empty), and an applied migration is never re-run, so the table is created here.
            // IF NOT EXISTS so that a hand-made table is left alone (its real column types were not compared with this definition).
            // The same definition is in main-database/custom-sql/app-structure-tables.sql.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS role2menu_item (
                    "PermissionRoleId" integer NOT NULL,
                    "ApplicationMenuItemId" integer NOT NULL,
                    "CanView" boolean NULL,
                    "CanEdit" boolean NULL,
                    "CanDelete" boolean NULL,
                    "CanCreate" boolean NULL,
                    "LatestUpdate" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT "PK_role2menu_item" PRIMARY KEY ("PermissionRoleId", "ApplicationMenuItemId")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
