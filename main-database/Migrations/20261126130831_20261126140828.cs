using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261126140828 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "user_message",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "MobileGpsDeviceId",
                table: "user_emergency_alert",
                newName: "GpsDeviceId");

            migrationBuilder.AddColumn<short>(
                name: "IconKey",
                table: "user2company",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "user_emergency_alert",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "user_emergency_alert",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "IconKeyOnMap",
                table: "user",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "KindId",
                table: "company",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "mission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorUserId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JoinKey = table.Column<string>(type: "text", nullable: false),
                    JoinQrCode = table.Column<string>(type: "text", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mission_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mission_primary_target_user_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_primary_target_user_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mission_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mission_details",
                columns: table => new
                {
                    MissionId = table.Column<int>(type: "integer", nullable: false),
                    IncidentName = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_details", x => x.MissionId);
                    table.ForeignKey(
                        name: "FK_mission_details_mission_MissionId",
                        column: x => x.MissionId,
                        principalTable: "mission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mission_primary_target_user",
                columns: table => new
                {
                    MissionId = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_primary_target_user", x => x.MissionId);
                    table.ForeignKey(
                        name: "FK_mission_primary_target_user_mission_MissionId",
                        column: x => x.MissionId,
                        principalTable: "mission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mission_uploaded_media",
                columns: table => new
                {
                    MissionId = table.Column<int>(type: "integer", nullable: false),
                    UploadedMediaId = table.Column<int>(type: "integer", nullable: false),
                    GroupKey = table.Column<short>(type: "smallint", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_uploaded_media", x => new { x.MissionId, x.UploadedMediaId });
                    table.ForeignKey(
                        name: "FK_mission_uploaded_media_mission_details_MissionId",
                        column: x => x.MissionId,
                        principalTable: "mission_details",
                        principalColumn: "MissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mission_uploaded_media_uploaded_media_UploadedMediaId",
                        column: x => x.UploadedMediaId,
                        principalTable: "uploaded_media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mission_CompanyId",
                table: "mission",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_mission_CreatorUserId_StatusId",
                table: "mission",
                columns: new[] { "CreatorUserId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_uploaded_media_UploadedMediaId",
                table: "mission_uploaded_media",
                column: "UploadedMediaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "user_message",
                newName: "TeamId");

            migrationBuilder.RenameColumn(
                name: "GpsDeviceId",
                table: "user_emergency_alert",
                newName: "MobileGpsDeviceId");

            migrationBuilder.AddColumn<float>(
                name: "CompleteLatitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CompleteLongitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Latitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Longitude",
                table: "user_emergency_alert",
                type: "real",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mobile_gps_device_map_state",
                columns: table => new
                {
                    MobileGpsDeviceId = table.Column<int>(type: "integer", nullable: false),
                    BatteryLevel = table.Column<float>(type: "real", nullable: true),
                    Dir = table.Column<short>(type: "smallint", nullable: true),
                    LatestOnMapUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    MotionActivity = table.Column<short>(type: "smallint", nullable: true),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    TeamId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_gps_device_map_state", x => x.MobileGpsDeviceId);
                });
        }
    }
}
