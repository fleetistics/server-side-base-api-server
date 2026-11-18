using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261117234259 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied. This migration was scaffolded against a stale snapshot and re-emitted everything that
            // 20261115232617..20261117143249 already do (column renames, SessionKey, CompanyId, UserName collation,
            // application_menu/_item/_page, client_application). Its Designer is identical to the one of
            // 20261117143249, so the model is unchanged and EF sees no pending changes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hand-emptied, see Up().
        }
    }
}
