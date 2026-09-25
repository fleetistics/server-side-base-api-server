using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260917230309 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_session_mobile_gps_device_MobileGpsDeviceId",
                table: "user_session");

            migrationBuilder.DropIndex(
                name: "IX_user_session_MobileGpsDeviceId",
                table: "user_session");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_session_source",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()"),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_session_source", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_session_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()"),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_session_status", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_session_MobileGpsDeviceId",
                table: "user_session",
                column: "MobileGpsDeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_session_mobile_gps_device_MobileGpsDeviceId",
                table: "user_session",
                column: "MobileGpsDeviceId",
                principalTable: "mobile_gps_device",
                principalColumn: "Id");
        }
    }
}
