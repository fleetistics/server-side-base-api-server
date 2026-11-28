using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class ContactCompanyOnLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContactCompanyId",
                table: "crm_company_user_contact",
                type: "integer",
                nullable: true);

            // Each contact's company moves from user.CompanyId to the link, and the user no longer "belongs" to it.
            migrationBuilder.Sql(@"
UPDATE crm_company_user_contact l SET ""ContactCompanyId"" = u.""CompanyId""
FROM ""user"" u WHERE u.""Id"" = l.""ContactUserId"" AND u.""CompanyId"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""user"" SET ""CompanyId"" = NULL WHERE ""KindId"" = 2;");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "crm_activity",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_crm_company_user_contact_ContactCompanyId",
                table: "crm_company_user_contact",
                column: "ContactCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_company_user_contact_OwnerCompanyId_ContactCompanyId",
                table: "crm_company_user_contact",
                columns: new[] { "OwnerCompanyId", "ContactCompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_user_contact_company_ContactCompanyId",
                table: "crm_company_user_contact",
                column: "ContactCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Contacts get their company back on the user; activities about a person without a company have nothing to point at.
            migrationBuilder.Sql(@"
UPDATE ""user"" u SET ""CompanyId"" = l.""ContactCompanyId""
FROM crm_company_user_contact l WHERE l.""ContactUserId"" = u.""Id"" AND l.""ContactCompanyId"" IS NOT NULL;");
            migrationBuilder.Sql(@"DELETE FROM crm_activity WHERE ""CompanyId"" IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_user_contact_company_ContactCompanyId",
                table: "crm_company_user_contact");

            migrationBuilder.DropIndex(
                name: "IX_crm_company_user_contact_ContactCompanyId",
                table: "crm_company_user_contact");

            migrationBuilder.DropIndex(
                name: "IX_crm_company_user_contact_OwnerCompanyId_ContactCompanyId",
                table: "crm_company_user_contact");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "crm_activity",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(name: "ContactCompanyId", table: "crm_company_user_contact");
        }
    }
}
