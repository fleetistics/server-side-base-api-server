using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261111222847 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settings_entry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Value = table.Column<string>(type: "jsonb", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings_entry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_settings_entry_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_settings_entry_CompanyId_Kind",
                table: "settings_entry",
                columns: new[] { "CompanyId", "Kind" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_settings_entry_Kind",
                table: "settings_entry",
                column: "Kind",
                unique: true,
                filter: "\"CompanyId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
