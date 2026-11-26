using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Snapshot-only: the schema changes (join invitation type, TypeId, JoinCode nullable, IsPersonal dropped,
    /// TeamId -> CompanyId renames, ...) were already applied by earlier migrations. This only refreshes the model snapshot.
    /// </remarks>
    public partial class ModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
