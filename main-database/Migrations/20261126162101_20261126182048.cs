using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261126182048 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_contact_company_ContactCompanyId",
                table: "company_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_company_contact_company_OwnerCompanyId",
                table: "company_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_company_contact_user_OwnerUserId",
                table: "company_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_company_info_company_CompanyId",
                table: "company_info");

            migrationBuilder.DropForeignKey(
                name: "FK_company_info_user_AccountManagerUserId",
                table: "company_info");

            migrationBuilder.DropForeignKey(
                name: "FK_company_info_user_PrimaryContactUserId",
                table: "company_info");

            migrationBuilder.DropForeignKey(
                name: "FK_company_location_company_CompanyId",
                table: "company_location");

            migrationBuilder.DropForeignKey(
                name: "FK_company_user_contact_company_OwnerCompanyId",
                table: "company_user_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_company_user_contact_user_ContactUserId",
                table: "company_user_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_company_user_contact_user_OwnerUserId",
                table: "company_user_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_user_info_user_UserId",
                table: "user_info");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_info",
                table: "user_info");

            migrationBuilder.DropPrimaryKey(
                name: "PK_payment_term",
                table: "payment_term");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lead_source",
                table: "lead_source");

            migrationBuilder.DropPrimaryKey(
                name: "PK_contact_type",
                table: "contact_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_user_contact",
                table: "company_user_contact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_type",
                table: "company_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_location",
                table: "company_location");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_info",
                table: "company_info");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_contact",
                table: "company_contact");

            migrationBuilder.RenameTable(
                name: "user_info",
                newName: "crm_user_info");

            migrationBuilder.RenameTable(
                name: "payment_term",
                newName: "crm_payment_term");

            migrationBuilder.RenameTable(
                name: "lead_source",
                newName: "crm_lead_source");

            migrationBuilder.RenameTable(
                name: "contact_type",
                newName: "crm_contact_type");

            migrationBuilder.RenameTable(
                name: "company_user_contact",
                newName: "crm_company_user_contact");

            migrationBuilder.RenameTable(
                name: "company_type",
                newName: "crm_company_type");

            migrationBuilder.RenameTable(
                name: "company_location",
                newName: "crm_company_location");

            migrationBuilder.RenameTable(
                name: "company_info",
                newName: "crm_company_info");

            migrationBuilder.RenameTable(
                name: "company_contact",
                newName: "crm_company_contact");

            migrationBuilder.RenameIndex(
                name: "IX_company_user_contact_OwnerUserId",
                table: "crm_company_user_contact",
                newName: "IX_crm_company_user_contact_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_user_contact_OwnerCompanyId_OwnerUserId",
                table: "crm_company_user_contact",
                newName: "IX_crm_company_user_contact_OwnerCompanyId_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_user_contact_ContactUserId",
                table: "crm_company_user_contact",
                newName: "IX_crm_company_user_contact_ContactUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_location_CompanyId",
                table: "crm_company_location",
                newName: "IX_crm_company_location_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_company_info_PrimaryContactUserId",
                table: "crm_company_info",
                newName: "IX_crm_company_info_PrimaryContactUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_info_AccountManagerUserId",
                table: "crm_company_info",
                newName: "IX_crm_company_info_AccountManagerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_contact_OwnerUserId",
                table: "crm_company_contact",
                newName: "IX_crm_company_contact_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_contact_OwnerCompanyId_OwnerUserId",
                table: "crm_company_contact",
                newName: "IX_crm_company_contact_OwnerCompanyId_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_company_contact_ContactCompanyId",
                table: "crm_company_contact",
                newName: "IX_crm_company_contact_ContactCompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_user_info",
                table: "crm_user_info",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_payment_term",
                table: "crm_payment_term",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_lead_source",
                table: "crm_lead_source",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_contact_type",
                table: "crm_contact_type",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_company_user_contact",
                table: "crm_company_user_contact",
                columns: new[] { "OwnerCompanyId", "ContactUserId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_company_type",
                table: "crm_company_type",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_company_location",
                table: "crm_company_location",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_company_info",
                table: "crm_company_info",
                column: "CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_crm_company_contact",
                table: "crm_company_contact",
                columns: new[] { "OwnerCompanyId", "ContactCompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_contact_company_ContactCompanyId",
                table: "crm_company_contact",
                column: "ContactCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_contact_company_OwnerCompanyId",
                table: "crm_company_contact",
                column: "OwnerCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_contact_user_OwnerUserId",
                table: "crm_company_contact",
                column: "OwnerUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_info_company_CompanyId",
                table: "crm_company_info",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_info_user_AccountManagerUserId",
                table: "crm_company_info",
                column: "AccountManagerUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_info_user_PrimaryContactUserId",
                table: "crm_company_info",
                column: "PrimaryContactUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_location_company_CompanyId",
                table: "crm_company_location",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_user_contact_company_OwnerCompanyId",
                table: "crm_company_user_contact",
                column: "OwnerCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_user_contact_user_ContactUserId",
                table: "crm_company_user_contact",
                column: "ContactUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_company_user_contact_user_OwnerUserId",
                table: "crm_company_user_contact",
                column: "OwnerUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_crm_user_info_user_UserId",
                table: "crm_user_info",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_contact_company_ContactCompanyId",
                table: "crm_company_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_contact_company_OwnerCompanyId",
                table: "crm_company_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_contact_user_OwnerUserId",
                table: "crm_company_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_info_company_CompanyId",
                table: "crm_company_info");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_info_user_AccountManagerUserId",
                table: "crm_company_info");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_info_user_PrimaryContactUserId",
                table: "crm_company_info");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_location_company_CompanyId",
                table: "crm_company_location");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_user_contact_company_OwnerCompanyId",
                table: "crm_company_user_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_user_contact_user_ContactUserId",
                table: "crm_company_user_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_company_user_contact_user_OwnerUserId",
                table: "crm_company_user_contact");

            migrationBuilder.DropForeignKey(
                name: "FK_crm_user_info_user_UserId",
                table: "crm_user_info");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_user_info",
                table: "crm_user_info");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_payment_term",
                table: "crm_payment_term");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_lead_source",
                table: "crm_lead_source");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_contact_type",
                table: "crm_contact_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_company_user_contact",
                table: "crm_company_user_contact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_company_type",
                table: "crm_company_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_company_location",
                table: "crm_company_location");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_company_info",
                table: "crm_company_info");

            migrationBuilder.DropPrimaryKey(
                name: "PK_crm_company_contact",
                table: "crm_company_contact");

            migrationBuilder.RenameTable(
                name: "crm_user_info",
                newName: "user_info");

            migrationBuilder.RenameTable(
                name: "crm_payment_term",
                newName: "payment_term");

            migrationBuilder.RenameTable(
                name: "crm_lead_source",
                newName: "lead_source");

            migrationBuilder.RenameTable(
                name: "crm_contact_type",
                newName: "contact_type");

            migrationBuilder.RenameTable(
                name: "crm_company_user_contact",
                newName: "company_user_contact");

            migrationBuilder.RenameTable(
                name: "crm_company_type",
                newName: "company_type");

            migrationBuilder.RenameTable(
                name: "crm_company_location",
                newName: "company_location");

            migrationBuilder.RenameTable(
                name: "crm_company_info",
                newName: "company_info");

            migrationBuilder.RenameTable(
                name: "crm_company_contact",
                newName: "company_contact");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_user_contact_OwnerUserId",
                table: "company_user_contact",
                newName: "IX_company_user_contact_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_user_contact_OwnerCompanyId_OwnerUserId",
                table: "company_user_contact",
                newName: "IX_company_user_contact_OwnerCompanyId_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_user_contact_ContactUserId",
                table: "company_user_contact",
                newName: "IX_company_user_contact_ContactUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_location_CompanyId",
                table: "company_location",
                newName: "IX_company_location_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_info_PrimaryContactUserId",
                table: "company_info",
                newName: "IX_company_info_PrimaryContactUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_info_AccountManagerUserId",
                table: "company_info",
                newName: "IX_company_info_AccountManagerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_contact_OwnerUserId",
                table: "company_contact",
                newName: "IX_company_contact_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_contact_OwnerCompanyId_OwnerUserId",
                table: "company_contact",
                newName: "IX_company_contact_OwnerCompanyId_OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_crm_company_contact_ContactCompanyId",
                table: "company_contact",
                newName: "IX_company_contact_ContactCompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_info",
                table: "user_info",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_payment_term",
                table: "payment_term",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_lead_source",
                table: "lead_source",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_contact_type",
                table: "contact_type",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_user_contact",
                table: "company_user_contact",
                columns: new[] { "OwnerCompanyId", "ContactUserId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_type",
                table: "company_type",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_location",
                table: "company_location",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_info",
                table: "company_info",
                column: "CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_contact",
                table: "company_contact",
                columns: new[] { "OwnerCompanyId", "ContactCompanyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_company_contact_company_ContactCompanyId",
                table: "company_contact",
                column: "ContactCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_contact_company_OwnerCompanyId",
                table: "company_contact",
                column: "OwnerCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_contact_user_OwnerUserId",
                table: "company_contact",
                column: "OwnerUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_company_info_company_CompanyId",
                table: "company_info",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_info_user_AccountManagerUserId",
                table: "company_info",
                column: "AccountManagerUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_company_info_user_PrimaryContactUserId",
                table: "company_info",
                column: "PrimaryContactUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_company_location_company_CompanyId",
                table: "company_location",
                column: "CompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_user_contact_company_OwnerCompanyId",
                table: "company_user_contact",
                column: "OwnerCompanyId",
                principalTable: "company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_user_contact_user_ContactUserId",
                table: "company_user_contact",
                column: "ContactUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_user_contact_user_OwnerUserId",
                table: "company_user_contact",
                column: "OwnerUserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_info_user_UserId",
                table: "user_info",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
