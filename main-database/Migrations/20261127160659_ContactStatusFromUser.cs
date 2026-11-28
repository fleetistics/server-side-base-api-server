using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <summary>
    /// A contact is now Active, Suspended or Archived through user.StatusId (1, 13, 14), like any user: crm_company_user_contact.IsArchived
    /// and crm_user_info.IsActive are removed. 13 was "Inactive" (the status every contact had) and is now "Suspended".
    /// Companies whose primary contact is not active get another active contact (the oldest shared one) or none.
    /// </summary>
    public partial class ContactStatusFromUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE user_status SET ""Name"" = 'Suspended' WHERE ""Id"" = 13;");
            migrationBuilder.Sql(@"INSERT INTO user_status (""Id"", ""Name"") VALUES (14, 'Archived') ON CONFLICT (""Id"") DO NOTHING;");

            // Archived link -> 14; else an active contact -> 1; else 13.
            migrationBuilder.Sql(@"
UPDATE ""user"" u
SET ""StatusId"" = CASE WHEN l.""IsArchived"" THEN 14 WHEN COALESCE(i.""IsActive"", TRUE) THEN 1 ELSE 13 END
FROM crm_company_user_contact l
LEFT JOIN crm_user_info i ON i.""UserId"" = l.""ContactUserId""
WHERE u.""Id"" = l.""ContactUserId"";");

            // A primary contact that is not active is replaced by the oldest other active shared contact of the company, or by nobody.
            migrationBuilder.Sql(@"
UPDATE crm_company_info ci
SET ""PrimaryContactUserId"" = (
        SELECT l.""ContactUserId"" FROM crm_company_user_contact l JOIN ""user"" u ON u.""Id"" = l.""ContactUserId""
        WHERE l.""ContactCompanyId"" = ci.""CompanyId"" AND l.""OwnerUserId"" IS NULL AND u.""StatusId"" = 1 AND l.""ContactUserId"" <> ci.""PrimaryContactUserId""
        ORDER BY l.""ContactUserId"" LIMIT 1),
    ""LatestUpdate"" = now()
WHERE ci.""PrimaryContactUserId"" IN (SELECT ""Id"" FROM ""user"" WHERE ""StatusId"" <> 1);");

            migrationBuilder.DropColumn(name: "IsArchived", table: "crm_company_user_contact");
            migrationBuilder.DropColumn(name: "IsActive", table: "crm_user_info");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "IsArchived", table: "crm_company_user_contact", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsActive", table: "crm_user_info", type: "boolean", nullable: false, defaultValue: true);

            migrationBuilder.Sql(@"UPDATE crm_company_user_contact l SET ""IsArchived"" = TRUE FROM ""user"" u WHERE u.""Id"" = l.""ContactUserId"" AND u.""StatusId"" = 14;");
            migrationBuilder.Sql(@"UPDATE crm_user_info i SET ""IsActive"" = FALSE FROM ""user"" u WHERE u.""Id"" = i.""UserId"" AND u.""StatusId"" <> 1 AND u.""KindId"" = 2;");
            // Every contact was "Inactive" (13), a user that cannot sign in.
            migrationBuilder.Sql(@"UPDATE ""user"" SET ""StatusId"" = 13 WHERE ""KindId"" = 2;");
            migrationBuilder.Sql(@"UPDATE user_status SET ""Name"" = 'Inactive' WHERE ""Id"" = 13;");
        }
    }
}
