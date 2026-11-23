using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261123140437 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_role2menu_item",
                table: "role2menu_item");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "role2menu_item",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "role2menu_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_role2menu_item",
                table: "role2menu_item",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UX_role2menu_item_company",
                table: "role2menu_item",
                columns: new[] { "PermissionRoleId", "ApplicationMenuItemId", "CompanyId" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_role2menu_item_default",
                table: "role2menu_item",
                columns: new[] { "PermissionRoleId", "ApplicationMenuItemId" },
                unique: true,
                filter: "\"CompanyId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_role2menu_item",
                table: "role2menu_item");

            migrationBuilder.DropIndex(
                name: "UX_role2menu_item_company",
                table: "role2menu_item");

            migrationBuilder.DropIndex(
                name: "UX_role2menu_item_default",
                table: "role2menu_item");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "role2menu_item");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "role2menu_item");

            migrationBuilder.AddPrimaryKey(
                name: "PK_role2menu_item",
                table: "role2menu_item",
                columns: new[] { "PermissionRoleId", "ApplicationMenuItemId" });
        }
    }
}
