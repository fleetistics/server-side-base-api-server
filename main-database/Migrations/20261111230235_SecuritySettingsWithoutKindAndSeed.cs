using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class SecuritySettingsWithoutKindAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_settings_entry_CompanyId_Kind",
                table: "settings_entry");

            migrationBuilder.DropIndex(
                name: "IX_settings_entry_Kind",
                table: "settings_entry");

            // Hand-written (the model differ never scripts a column drop): Kind is NOT NULL without a default,
            // so it has to go before the seed row below can be inserted.
            migrationBuilder.Sql("ALTER TABLE settings_entry DROP COLUMN IF EXISTS \"Kind\";");

            migrationBuilder.InsertData(
                table: "settings_entry",
                columns: new[] { "Id", "CompanyId", "Value" },
                values: new object[] { 1, null, "{\"password\":{\"minLength\":12,\"requireUpperAndLowerCase\":true,\"requireNumber\":true,\"requireSymbol\":true,\"disallowNameOrEmail\":true,\"screenAgainstBreachedLists\":true,\"resetFrequencyDays\":90,\"historyCount\":5,\"minAgeDays\":1},\"twoFactor\":{\"required\":true,\"appliesTo\":1,\"allowAuthenticatorApp\":true,\"allowEmailOneTimeCode\":false,\"allowBackupCodes\":true,\"allowSmsMessage\":true,\"allowSecurityKeyPasskey\":true,\"enrollmentGraceDays\":7,\"rememberDeviceDays\":30},\"emailDomain\":{\"domainOnlyAccounts\":true,\"justInTimeProvisioning\":true,\"scimDirectorySync\":false},\"sessions\":{\"idleTimeoutMinutes\":30,\"maxSessionHours\":12,\"concurrentSessions\":3,\"lockoutAttempts\":5,\"lockoutMinutes\":15,\"stepUpReauthentication\":true,\"ipAllowlistEnabled\":false,\"geoBlockingEnabled\":true,\"deviceTrustForAdminRoles\":true,\"captchaOnRepeatedFailures\":true,\"captchaAfterFailedAttempts\":3}}" });

            migrationBuilder.CreateIndex(
                name: "IX_settings_entry_CompanyId",
                table: "settings_entry",
                column: "CompanyId",
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            // Hand-written, not in the model: a unique index treats NULL CompanyIds as distinct, so the one above
            // cannot stop a second system-wide row. This index has the same value (true) for every row with a
            // NULL CompanyId, which allows exactly one of them.
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_settings_entry_single_system_row\" ON settings_entry ((true)) WHERE \"CompanyId\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_settings_entry_single_system_row\";");

            migrationBuilder.DropIndex(
                name: "IX_settings_entry_CompanyId",
                table: "settings_entry");

            migrationBuilder.DeleteData(
                table: "settings_entry",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<short>(
                name: "Kind",
                table: "settings_entry",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateIndex(
                name: "IX_settings_entry_CompanyId_Kind",
                table: "settings_entry",
                columns: new[] { "CompanyId", "Kind" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_settings_entry_Kind",
                table: "settings_entry",
                column: "Kind",
                unique: true,
                filter: "\"CompanyId\" IS NULL");
        }
    }
}
