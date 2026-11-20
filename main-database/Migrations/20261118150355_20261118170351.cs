using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20261118170351 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_session_SessionKey",
                table: "user_session");

            migrationBuilder.RenameColumn(
                name: "SessionKey",
                table: "user_session",
                newName: "ClientInfo_DeviceUID");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_PlatformDetails",
                table: "user_session",
                newName: "ClientInfo_CodeVersion");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_AppHash",
                table: "user_session",
                newName: "ClientInfo_AppUID");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "client_application",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientInfo_DeviceUID",
                table: "user_session",
                newName: "SessionKey");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_CodeVersion",
                table: "user_session",
                newName: "ClientInfo_PlatformDetails");

            migrationBuilder.RenameColumn(
                name: "ClientInfo_AppUID",
                table: "user_session",
                newName: "ClientInfo_AppHash");

            migrationBuilder.AddColumn<short>(
                name: "ClientInfo_ClientApplicationId",
                table: "user_session",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "user_session",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "Id",
                table: "client_application",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "Uid",
                table: "client_application",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_user_session_SessionKey",
                table: "user_session",
                column: "SessionKey",
                unique: true);
        }
    }
}
