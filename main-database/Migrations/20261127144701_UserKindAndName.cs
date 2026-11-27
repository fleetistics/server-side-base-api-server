using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <summary>
    /// user.FullName becomes FirstName + LastName and user gets KindId (1 User, 2 Contact, 3 Test).
    /// FullName is renamed to FirstName first (so no data is lost) and then split at the first space;
    /// contact people take their exact names from crm_user_info. KindId is filled from UserSourceId.
    /// </summary>
    public partial class UserKindAndName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "FullName", table: "user", newName: "FirstName");

            migrationBuilder.AddColumn<string>(name: "LastName", table: "user", type: "text", nullable: true);

            // The default stays: a plain INSERT that does not name the column makes an ordinary user.
            migrationBuilder.AddColumn<short>(name: "KindId", table: "user", type: "smallint", nullable: false, defaultValue: (short)1);

            // "Mary Ann Smith" gives "Mary" and "Ann Smith"; a single word stays a first name.
            migrationBuilder.Sql(@"
UPDATE ""user""
SET ""LastName"" = NULLIF(btrim(substr(btrim(""FirstName""), position(' ' in btrim(""FirstName"")) + 1)), ''),
    ""FirstName"" = split_part(btrim(""FirstName""), ' ', 1)
WHERE position(' ' in btrim(""FirstName"")) > 0;");

            migrationBuilder.Sql(@"
UPDATE ""user"" u
SET ""FirstName"" = i.""FirstName"", ""LastName"" = NULLIF(btrim(i.""LastName""), '')
FROM crm_user_info i
WHERE i.""UserId"" = u.""Id"" AND u.""UserSourceId"" = 6 AND NULLIF(btrim(i.""FirstName""), '') IS NOT NULL;");

            migrationBuilder.Sql(@"UPDATE ""user"" SET ""KindId"" = 2 WHERE ""UserSourceId"" = 6;");
            migrationBuilder.Sql(@"UPDATE ""user"" SET ""KindId"" = 3 WHERE ""UserSourceId"" IN (12, 13, 14);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""user"" SET ""FirstName"" = btrim(""FirstName"" || ' ' || COALESCE(""LastName"", ''));");

            migrationBuilder.DropColumn(name: "KindId", table: "user");
            migrationBuilder.DropColumn(name: "LastName", table: "user");
            migrationBuilder.RenameColumn(name: "FirstName", table: "user", newName: "FullName");
        }
    }
}
