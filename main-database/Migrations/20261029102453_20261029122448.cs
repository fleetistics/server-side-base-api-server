using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261029122448 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // gps_device_map_status used to hold the activity values (Moving, Idle ...). That table is renamed, and the
            // name is reused for the new Invisible/Visible table. The old trigger is renamed with it.
            migrationBuilder.Sql(@"ALTER TABLE gps_device_map_status RENAME TO gps_device_activity_status;
ALTER TABLE gps_device_activity_status RENAME CONSTRAINT ""PK_gps_device_map_status"" TO ""PK_gps_device_activity_status"";
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_gps_device_map_status_latest_update' AND tgrelid = 'gps_device_activity_status'::regclass) THEN
        ALTER TRIGGER trg_gps_device_map_status_latest_update ON gps_device_activity_status RENAME TO trg_gps_device_activity_status_latest_update;
    END IF;
END $$;");

            migrationBuilder.CreateTable(
                name: "gps_device_map_status",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_map_status", x => x.Id);
                });

            migrationBuilder.Sql(
                "INSERT INTO gps_device_map_status (\"Id\", \"Name\") VALUES (0, 'Invisible'), (1, 'Visible') ON CONFLICT (\"Id\") DO NOTHING;");

            migrationBuilder.CreateTable(
                name: "gps_asset_type",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_asset_type", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "gps_asset_type",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Phone" },
                    { 2, "Car" },
                    { 3, "Pickup" },
                    { 4, "Truck" },
                    { 5, "Van" },
                    { 6, "Bus" },
                    { 7, "Motorcycle" },
                    { 8, "Trailer" },
                    { 9, "Equipment" },
                    { 10, "Other" }
                });

            migrationBuilder.AddColumn<int>(
                name: "AssetTypeId",
                table: "gps_device",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gps_device_AssetTypeId",
                table: "gps_device",
                column: "AssetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_gps_asset_type_Name",
                table: "gps_asset_type",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_gps_device_gps_asset_type_AssetTypeId",
                table: "gps_device",
                column: "AssetTypeId",
                principalTable: "gps_asset_type",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Map state: what StatusId held (the activity) moves to ActivityStatusId, and StatusId becomes visibility.
            migrationBuilder.AddColumn<short>(
                name: "ActivityStatusId",
                table: "gps_device_map_state",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "WarningFlags",
                table: "gps_device_map_state",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "LocationAccuracyId",
                table: "gps_device_map_state",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.Sql(@"UPDATE gps_device_map_state SET
    ""ActivityStatusId"" = ""StatusId"",
    ""WarningFlags"" = (CASE WHEN ""LowBattery"" THEN 1 ELSE 0 END) | (CASE WHEN ""EngineFault"" THEN 2 ELSE 0 END),
    ""StatusId"" = 1;");

            migrationBuilder.AlterColumn<short>(
                name: "StatusId",
                table: "gps_device_map_state",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1,
                oldClrType: typeof(short),
                oldType: "smallint");

            // StatusChangedAt, LastCommunicationAt, LowBattery and EngineFault are no longer mapped. The columns stay for one
            // release (this repo never drops in a migration) but must not block an insert that does not know them.
            migrationBuilder.Sql(@"ALTER TABLE gps_device_map_state
    ALTER COLUMN ""StatusChangedAt"" DROP NOT NULL,
    ALTER COLUMN ""LastCommunicationAt"" DROP NOT NULL,
    ALTER COLUMN ""LowBattery"" SET DEFAULT false,
    ALTER COLUMN ""EngineFault"" SET DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE gps_device_map_state SET
    ""StatusChangedAt"" = COALESCE(""StatusChangedAt"", ""LastPositionAt""),
    ""LastCommunicationAt"" = COALESCE(""LastCommunicationAt"", ""LastPositionAt""),
    ""LowBattery"" = (""WarningFlags"" & 1) <> 0,
    ""EngineFault"" = (""WarningFlags"" & 2) <> 0,
    ""StatusId"" = ""ActivityStatusId"";
ALTER TABLE gps_device_map_state
    ALTER COLUMN ""StatusChangedAt"" SET NOT NULL,
    ALTER COLUMN ""LastCommunicationAt"" SET NOT NULL,
    ALTER COLUMN ""LowBattery"" DROP DEFAULT,
    ALTER COLUMN ""EngineFault"" DROP DEFAULT;");

            migrationBuilder.AlterColumn<short>(
                name: "StatusId",
                table: "gps_device_map_state",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)1);

            migrationBuilder.DropColumn(
                name: "ActivityStatusId",
                table: "gps_device_map_state");

            migrationBuilder.DropColumn(
                name: "WarningFlags",
                table: "gps_device_map_state");

            migrationBuilder.DropColumn(
                name: "LocationAccuracyId",
                table: "gps_device_map_state");

            migrationBuilder.DropForeignKey(
                name: "FK_gps_device_gps_asset_type_AssetTypeId",
                table: "gps_device");

            migrationBuilder.DropIndex(
                name: "IX_gps_device_AssetTypeId",
                table: "gps_device");

            migrationBuilder.DropColumn(
                name: "AssetTypeId",
                table: "gps_device");

            migrationBuilder.DropTable(
                name: "gps_asset_type");

            migrationBuilder.DropTable(
                name: "gps_device_map_status");

            migrationBuilder.Sql(@"ALTER TABLE gps_device_activity_status RENAME TO gps_device_map_status;
ALTER TABLE gps_device_map_status RENAME CONSTRAINT ""PK_gps_device_activity_status"" TO ""PK_gps_device_map_status"";
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_gps_device_activity_status_latest_update' AND tgrelid = 'gps_device_map_status'::regclass) THEN
        ALTER TRIGGER trg_gps_device_activity_status_latest_update ON gps_device_map_status RENAME TO trg_gps_device_map_status_latest_update;
    END IF;
END $$;");
        }
    }
}
