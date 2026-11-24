using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class JoinInvitationTypeAndPlainCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_join_invitation_type",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LatestUpdate = table.Column<DateTime>(type: "timestamp with time zone", rowVersion: true, nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_join_invitation_type", x => x.Id);
                });

            migrationBuilder.AlterColumn<string>(
                name: "JoinCode",
                table: "user_join_invitation",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // 1 = Company (the anonymous code), 2 = User; Personal (3) is not made yet.
            migrationBuilder.AddColumn<short>(
                name: "TypeId",
                table: "user_join_invitation",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            // The old codes were hashes and cannot be used any more: the rows that were open are cancelled and the company
            // gets a new anonymous row the first time its Invites & Join tab is opened.
            migrationBuilder.Sql(@"UPDATE user_join_invitation SET ""TypeId"" = CASE WHEN ""IsPersonal"" IS TRUE THEN 2 ELSE 1 END;");
            migrationBuilder.Sql(@"UPDATE user_join_invitation SET ""StatusId"" = 4 WHERE ""StatusId"" = 1;");
            migrationBuilder.Sql(@"UPDATE user_join_invitation SET ""JoinCode"" = NULL;");

            migrationBuilder.DropColumn(
                name: "IsPersonal",
                table: "user_join_invitation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPersonal",
                table: "user_join_invitation",
                type: "boolean",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE user_join_invitation SET ""IsPersonal"" = (""TypeId"" <> 1);");
            migrationBuilder.Sql(@"UPDATE user_join_invitation SET ""JoinCode"" = 'migrated-' || ""Id"" WHERE ""JoinCode"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE user_join_invitation SET ""StatusId"" = 4 WHERE ""StatusId"" = 5;");

            migrationBuilder.AlterColumn<string>(
                name: "JoinCode",
                table: "user_join_invitation",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "user_join_invitation");

            migrationBuilder.DropTable(
                name: "user_join_invitation_type");
        }
    }
}
