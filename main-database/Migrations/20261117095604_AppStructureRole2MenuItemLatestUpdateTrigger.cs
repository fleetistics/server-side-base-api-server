using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class AppStructureRole2MenuItemLatestUpdateTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // role2menu_item was created by raw SQL in 20261117093526_AppStructureRole2MenuItemCreate (already applied), which
            // skipped the LatestUpdate trigger that the migration SQL generator adds for CreateTable. This is the same trigger
            // the other app structure tables have. CREATE OR REPLACE so it is safe if the trigger already exists.
            migrationBuilder.Sql("""
                CREATE OR REPLACE TRIGGER trg_role2menu_item_latest_update
                BEFORE UPDATE ON "role2menu_item"
                FOR EACH ROW EXECUTE FUNCTION update_latestupdate_column();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
