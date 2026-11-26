using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261125200748 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        }
    }
}
