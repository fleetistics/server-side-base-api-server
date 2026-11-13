using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261112085618 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permission_role",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_role", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_role_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_group",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    GroupColor = table.Column<short>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_group", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_group_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_join_invitation_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_join_invitation_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_join_request_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_join_request_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user2company_join_type",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user2company_join_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user2company_status",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user2company_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user2company",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PermissionRoleId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    JoinTypeId = table.Column<short>(type: "smallint", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user2company", x => new { x.CompanyId, x.UserId });
                    table.ForeignKey(
                        name: "FK_user2company_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user2company_permission_role_PermissionRoleId",
                        column: x => x.PermissionRoleId,
                        principalTable: "permission_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user2company_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_join_invitation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    JoinCode = table.Column<string>(type: "text", nullable: false),
                    JoinQrCodeImageName = table.Column<string>(type: "text", nullable: false),
                    PermissionRoleId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    IsPersonal = table.Column<bool>(type: "boolean", nullable: true),
                    IsAutoAppRegistrationAllowed = table.Column<bool>(type: "boolean", nullable: true),
                    DoesNotRequireAdminApproval = table.Column<bool>(type: "boolean", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_join_invitation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_join_invitation_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_join_invitation_permission_role_PermissionRoleId",
                        column: x => x.PermissionRoleId,
                        principalTable: "permission_role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_join_invitation_user_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_join_invitation_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_join_invitation_user_group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "user_group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user2group",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user2group", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_user2group_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user2group_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user2group_user_group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "user_group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_join_request",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    UserJoinInvitationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<short>(type: "smallint", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HandledByCompanyUserId = table.Column<int>(type: "integer", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_join_request", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_join_request_company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_join_request_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_join_request_user_join_invitation_UserJoinInvitationId",
                        column: x => x.UserJoinInvitationId,
                        principalTable: "user_join_invitation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permission_role_CompanyId",
                table: "permission_role",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_group_CompanyId",
                table: "user_group",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_invitation_CompanyId",
                table: "user_join_invitation",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_invitation_CreatedByUserId",
                table: "user_join_invitation",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_invitation_GroupId",
                table: "user_join_invitation",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_invitation_JoinCode",
                table: "user_join_invitation",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_join_invitation_PermissionRoleId",
                table: "user_join_invitation",
                column: "PermissionRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_invitation_UserId",
                table: "user_join_invitation",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_request_CompanyId_StatusId",
                table: "user_join_request",
                columns: new[] { "CompanyId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_join_request_UserId",
                table: "user_join_request",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_join_request_UserJoinInvitationId",
                table: "user_join_request",
                column: "UserJoinInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_user2company_PermissionRoleId",
                table: "user2company",
                column: "PermissionRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user2company_UserId",
                table: "user2company",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user2group_CompanyId_UserId",
                table: "user2group",
                columns: new[] { "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_user2group_UserId",
                table: "user2group",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
