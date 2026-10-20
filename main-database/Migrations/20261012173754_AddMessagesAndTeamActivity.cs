using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagesAndTeamActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_activity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    ActivityTypeId = table.Column<short>(type: "smallint", nullable: false),
                    Location = table.Column<Point>(type: "geometry", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_activity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "team_activity_type",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_activity_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_message",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToUserId = table.Column<int>(type: "integer", nullable: true),
                    TeamId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_message", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_message_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_message_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_message2user_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_message2user_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "team_activity2user",
                columns: table => new
                {
                    TeamActivityId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_activity2user", x => new { x.TeamActivityId, x.UserId });
                    table.ForeignKey(
                        name: "FK_team_activity2user_team_activity_TeamActivityId",
                        column: x => x.TeamActivityId,
                        principalTable: "team_activity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_message2user",
                columns: table => new
                {
                    UserMessageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_message2user", x => new { x.UserMessageId, x.UserId });
                    table.ForeignKey(
                        name: "FK_user_message2user_user_message_UserMessageId",
                        column: x => x.UserMessageId,
                        principalTable: "user_message",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
