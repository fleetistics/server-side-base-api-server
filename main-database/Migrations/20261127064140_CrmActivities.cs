using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class CrmActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crm_activity_priority",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activity_priority", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_activity_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activity_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_activity_type",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activity_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_recurrence_frequency",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_recurrence_frequency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_activity_series",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerCompanyId = table.Column<int>(type: "integer", nullable: false),
                    FrequencyId = table.Column<short>(type: "smallint", nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    EndAfterCount = table.Column<int>(type: "integer", nullable: true),
                    EndOnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activity_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_activity_series_company_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_activity_series_crm_recurrence_frequency_FrequencyId",
                        column: x => x.FrequencyId,
                        principalTable: "crm_recurrence_frequency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crm_activity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerCompanyId = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    TypeId = table.Column<short>(type: "smallint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PriorityId = table.Column<short>(type: "smallint", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ContactUserId = table.Column<int>(type: "integer", nullable: true),
                    AssignedToUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_activity_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_activity_company_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_activity_crm_activity_priority_PriorityId",
                        column: x => x.PriorityId,
                        principalTable: "crm_activity_priority",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crm_activity_crm_activity_series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "crm_activity_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_activity_crm_activity_status_StatusId",
                        column: x => x.StatusId,
                        principalTable: "crm_activity_status",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crm_activity_crm_activity_type_TypeId",
                        column: x => x.TypeId,
                        principalTable: "crm_activity_type",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crm_activity_user_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_activity_user_ContactUserId",
                        column: x => x.ContactUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_activity_user_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_activity_user_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_AssignedToUserId",
                table: "crm_activity",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_CompanyId",
                table: "crm_activity",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_ContactUserId",
                table: "crm_activity",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_CreatedByUserId",
                table: "crm_activity",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_OwnerCompanyId_StartAt",
                table: "crm_activity",
                columns: new[] { "OwnerCompanyId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_OwnerUserId",
                table: "crm_activity",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_PriorityId",
                table: "crm_activity",
                column: "PriorityId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_SeriesId",
                table: "crm_activity",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_StatusId",
                table: "crm_activity",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_TypeId",
                table: "crm_activity",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_series_FrequencyId",
                table: "crm_activity_series",
                column: "FrequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activity_series_OwnerCompanyId",
                table: "crm_activity_series",
                column: "OwnerCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "crm_activity");
            migrationBuilder.DropTable(name: "crm_activity_series");
            migrationBuilder.DropTable(name: "crm_activity_priority");
            migrationBuilder.DropTable(name: "crm_activity_status");
            migrationBuilder.DropTable(name: "crm_activity_type");
            migrationBuilder.DropTable(name: "crm_recurrence_frequency");
        }
    }
}
