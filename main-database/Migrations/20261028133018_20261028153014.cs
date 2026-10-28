using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261028153014 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "gps_device_map_state",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                table: "gps_device",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gps_device_type",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_type", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gps_device_type_gps_device_type_ParentId",
                        column: x => x.ParentId,
                        principalTable: "gps_device_type",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "gps_device_type",
                columns: new[] { "Id", "Name", "ParentId", "ProviderId" },
                values: new object[,]
                {
                    { 1, "Android", null, 4 },
                    { 2, "iOS", null, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_ProviderId_SerialNumber",
                table: "gps_device",
                columns: new[] { "ProviderId", "SerialNumber" },
                unique: true,
                filter: "\"ProviderId\" = 4");

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_TypeId",
                table: "gps_device",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_type_ParentId",
                table: "gps_device_type",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_type_ProviderId_Name",
                table: "gps_device_type",
                columns: new[] { "ProviderId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_gps_device_gps_device_type_TypeId",
                table: "gps_device",
                column: "TypeId",
                principalTable: "gps_device_type",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Phones move from mobile_gps_device into gps_device KEEPING THEIR IDS, so session tokens, emergency
            // alerts, track points and map states that carry the id stay valid. Old provider 1 (Ios) / 2 (Android)
            // become provider Mobile (4) plus the matching device type. Idempotent. The old tables are no longer
            // mapped but are deliberately left in place for one release as a safety net (drop them by hand later).
            migrationBuilder.Sql(
                "INSERT INTO gps_device_provider (\"Id\", \"Name\") VALUES (4, 'Mobile') ON CONFLICT (\"Id\") DO NOTHING;");

            migrationBuilder.Sql(
                "INSERT INTO gps_device (\"Id\", \"ProviderId\", \"TypeId\", \"StatusId\", \"SerialNumber\", \"LatestUpdate\", \"UserId\", \"Name\") " +
                "OVERRIDING SYSTEM VALUE " +
                "SELECT m.\"Id\", 4, CASE m.\"ProviderId\" WHEN 1 THEN 2 WHEN 2 THEN 1 END, m.\"StatusId\", m.\"DeviceUID\", m.\"LatestUpdate\", m.\"UserId\", m.\"Name\" " +
                "FROM mobile_gps_device m ON CONFLICT (\"Id\") DO NOTHING;");

            // Explicit ids were inserted above (and into gps_device_type by the seed): move both identity
            // sequences clear of them. 1000 leaves room so a hand-made row can never collide with a later one.
            // DO + PERFORM, not a bare SELECT: `dotnet ef migrations script --idempotent` wraps every statement in a
            // PL/pgSQL block, where a SELECT without a destination is an error (and rolls the whole script back).
            migrationBuilder.Sql(
                "DO $$ BEGIN PERFORM setval(pg_get_serial_sequence('gps_device', 'Id'), GREATEST((SELECT COALESCE(MAX(\"Id\"), 0) FROM gps_device), 1000)); END $$;");
            migrationBuilder.Sql(
                "DO $$ BEGIN PERFORM setval(pg_get_serial_sequence('gps_device_type', 'Id'), GREATEST((SELECT COALESCE(MAX(\"Id\"), 0) FROM gps_device_type), 1000)); END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gps_device_gps_device_type_TypeId",
                table: "gps_device");

            migrationBuilder.DropIndex(
                name: "IX_gps_device_ProviderId_SerialNumber",
                table: "gps_device");

            migrationBuilder.DropIndex(
                name: "IX_gps_device_TypeId",
                table: "gps_device");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "gps_device_map_state",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "gps_device");

            migrationBuilder.DropTable(
                name: "gps_device_type");

            // Up never dropped mobile_gps_device / mobile_gps_device_provider (see the comment there), so there is
            // nothing to recreate. The copied rows stay in gps_device; remove them by hand if you really roll back.
        }
    }
}
