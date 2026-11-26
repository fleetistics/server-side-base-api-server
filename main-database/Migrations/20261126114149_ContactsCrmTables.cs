using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class ContactsCrmTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_contact",
                columns: table => new
                {
                    OwnerCompanyId = table.Column<int>(type: "integer", nullable: false),
                    ContactCompanyId = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_contact", x => new { x.OwnerCompanyId, x.ContactCompanyId });
                    table.ForeignKey(
                        name: "FK_company_contact_company_ContactCompanyId",
                        column: x => x.ContactCompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_contact_company_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_contact_user_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "company_info",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<short>(type: "smallint", nullable: true),
                    LegalName = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    MainPhone = table.Column<string>(type: "text", nullable: true),
                    AccountNumber = table.Column<string>(type: "text", nullable: true),
                    AccountManagerUserId = table.Column<int>(type: "integer", nullable: true),
                    PaymentTermId = table.Column<short>(type: "smallint", nullable: true),
                    TaxId = table.Column<string>(type: "text", nullable: true),
                    Employees = table.Column<int>(type: "integer", nullable: true),
                    FleetVehicles = table.Column<int>(type: "integer", nullable: true),
                    CustomerSince = table.Column<DateOnly>(type: "date", nullable: true),
                    AnnualRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Territory = table.Column<string>(type: "text", nullable: true),
                    LeadSourceId = table.Column<short>(type: "smallint", nullable: true),
                    PrimaryContactUserId = table.Column<int>(type: "integer", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_info", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_company_info_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_info_user_AccountManagerUserId",
                        column: x => x.AccountManagerUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_company_info_user_PrimaryContactUserId",
                        column: x => x.PrimaryContactUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "company_location",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Street = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Zip = table.Column<string>(type: "text", nullable: true),
                    IsJobSite = table.Column<bool>(type: "boolean", nullable: false),
                    IsBilling = table.Column<bool>(type: "boolean", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_location", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_location_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company_type",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "company_user_contact",
                columns: table => new
                {
                    OwnerCompanyId = table.Column<int>(type: "integer", nullable: false),
                    ContactUserId = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_user_contact", x => new { x.OwnerCompanyId, x.ContactUserId });
                    table.ForeignKey(
                        name: "FK_company_user_contact_company_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_user_contact_user_ContactUserId",
                        column: x => x.ContactUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_user_contact_user_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contact_type",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lead_source",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_source", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_term",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_term", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_info",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Mobile = table.Column<string>(type: "text", nullable: true),
                    ContactTypeId = table.Column<short>(type: "smallint", nullable: true),
                    LeadSourceId = table.Column<short>(type: "smallint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_info", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_user_info_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_contact_ContactCompanyId",
                table: "company_contact",
                column: "ContactCompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_contact_OwnerCompanyId_OwnerUserId",
                table: "company_contact",
                columns: new[] { "OwnerCompanyId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_company_contact_OwnerUserId",
                table: "company_contact",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_info_AccountManagerUserId",
                table: "company_info",
                column: "AccountManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_info_PrimaryContactUserId",
                table: "company_info",
                column: "PrimaryContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_location_CompanyId",
                table: "company_location",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_company_user_contact_ContactUserId",
                table: "company_user_contact",
                column: "ContactUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_user_contact_OwnerCompanyId_OwnerUserId",
                table: "company_user_contact",
                columns: new[] { "OwnerCompanyId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_company_user_contact_OwnerUserId",
                table: "company_user_contact",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_contact");

            migrationBuilder.DropTable(
                name: "company_user_contact");

            migrationBuilder.DropTable(
                name: "company_location");

            migrationBuilder.DropTable(
                name: "company_info");

            migrationBuilder.DropTable(
                name: "user_info");

            migrationBuilder.DropTable(
                name: "company_type");

            migrationBuilder.DropTable(
                name: "contact_type");

            migrationBuilder.DropTable(
                name: "lead_source");

            migrationBuilder.DropTable(
                name: "payment_term");
        }
    }
}
