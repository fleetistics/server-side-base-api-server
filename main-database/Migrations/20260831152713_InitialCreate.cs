using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION update_latestupdate_column()
                RETURNS trigger AS $$
                BEGIN
                    NEW."LatestUpdate" = now();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.CreateTable(
                name: "client_device_platform",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PlatformKeys = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_device_platform", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_log_record",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Log = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_log_record", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gps_device",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    SerialNumber = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()"),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gps_device_provider",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_provider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gps_device_status",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gps_device_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "language",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    EnglishName = table.Column<string>(type: "text", nullable: false),
                    NativeName = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_language", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "translation_token",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_token", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "uploaded_media",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    PreviewFileName = table.Column<string>(type: "text", nullable: true),
                    MediaType = table.Column<byte>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_media", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_session_source",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
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
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_session_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_source",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_source", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_status",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_session",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GpsDeviceId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    SessionSourceId = table.Column<short>(type: "smallint", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()"),
                    ClientInfo_AppUID = table.Column<string>(type: "text", nullable: false),
                    ClientInfo_AppVersion = table.Column<string>(type: "text", nullable: false),
                    ClientInfo_ClientDevicePlatformId = table.Column<short>(type: "smallint", nullable: false),
                    ClientInfo_CodeVersion = table.Column<string>(type: "text", nullable: false),
                    ClientInfo_DeviceUID = table.Column<string>(type: "text", nullable: false),
                    ClientInfo_FCMToken = table.Column<string>(type: "text", nullable: false),
                    Token_CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Token_ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Token_LatestRefreshTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Token_PreviousSessionRotationKey = table.Column<string>(type: "text", nullable: false),
                    Token_SessionRotationKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_session_gps_device_GpsDeviceId",
                        column: x => x.GpsDeviceId,
                        principalTable: "gps_device",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "translation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    TranslatedText = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_language_LanguageCode",
                        column: x => x.LanguageCode,
                        principalTable: "language",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_translation_translation_token_TokenId",
                        column: x => x.TokenId,
                        principalTable: "translation_token",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()"),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    UserSourceId = table.Column<short>(type: "smallint", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: true),
                    IsPrivateMode = table.Column<bool>(type: "boolean", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    AvatarImageId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_uploaded_media_AvatarImageId",
                        column: x => x.AvatarImageId,
                        principalTable: "uploaded_media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_translation_LanguageCode",
                table: "translation",
                column: "LanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_translation_TokenId_LanguageCode",
                table: "translation",
                columns: new[] { "TokenId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_token_Text",
                table: "translation_token",
                column: "Text",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_AvatarImageId",
                table: "user",
                column: "AvatarImageId");

            migrationBuilder.CreateIndex(
                name: "IX_user_session_GpsDeviceId",
                table: "user_session",
                column: "GpsDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
