using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <summary>
    /// The tables themselves (and their keys and indexes) were renamed to the crm_ prefix by the previous migration, which the
    /// Debug build generated. EF does not rename what hangs off a table without a model change, so this finishes the job by hand:
    /// the LatestUpdate triggers and the company_location id sequence get the crm_ name too.
    /// </summary>
    public partial class CrmTablePrefix : Migration
    {
        private static readonly string[] Tables =
        {
            "company_info", "user_info", "company_location", "company_contact", "company_user_contact",
            "company_type", "contact_type", "lead_source", "payment_term",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "", "crm_");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "crm_", "");

        /// <summary>From the names with prefix <paramref name="from"/> to the ones with <paramref name="to"/>.</summary>
        private static void Rename(MigrationBuilder migrationBuilder, string from, string to)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"ALTER TRIGGER trg_{from}{table}_latest_update ON ""{to}{table}"" RENAME TO trg_{to}{table}_latest_update;");
            }

            migrationBuilder.Sql($@"ALTER SEQUENCE IF EXISTS ""{from}company_location_Id_seq"" RENAME TO ""{to}company_location_Id_seq"";");
        }
    }
}
