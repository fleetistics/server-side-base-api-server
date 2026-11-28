using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class ContactEmployeeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "EmployeeStatusId",
                table: "crm_user_info",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "crm_employee_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_employee_status", x => x.Id);
                });

            migrationBuilder.Sql(@"
INSERT INTO crm_employee_status (""Id"", ""Name"") VALUES (1, 'Office'), (2, 'Manager'), (3, 'Driver'), (4, 'Warehouse')
ON CONFLICT (""Id"") DO NOTHING;");

            // The contact types are Contact, Customer, Vendor and Employee (Partner is gone). Names are trimmed: one had a line break at its end.
            migrationBuilder.Sql(@"
INSERT INTO crm_contact_type (""Id"", ""Name"") VALUES (1, 'Contact'), (2, 'Customer'), (3, 'Vendor'), (4, 'Employee')
ON CONFLICT (""Id"") DO UPDATE SET ""Name"" = EXCLUDED.""Name"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "crm_employee_status");
            migrationBuilder.DropColumn(name: "EmployeeStatusId", table: "crm_user_info");
        }
    }
}
