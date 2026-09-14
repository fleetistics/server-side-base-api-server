using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260912093143 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LatestUpdate",
                table: "team",
                type: "timestamp with time zone",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
