using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace main_database.Migrations
{
    /// <inheritdoc />
    public partial class _20260907142649 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Postgres has no implicit text->smallint cast, so AlterColumn's default
            // ALTER COLUMN ... TYPE statement fails; these columns were only just added
            // by the prior migration and are empty, so a plain ::smallint cast is safe.
            migrationBuilder.Sql(
                "ALTER TABLE mobile_gps_device_track_point ALTER COLUMN \"MotionActivity\" TYPE smallint USING \"MotionActivity\"::smallint;");

            migrationBuilder.Sql(
                "ALTER TABLE mobile_gps_device_map_state ALTER COLUMN \"MotionActivity\" TYPE smallint USING \"MotionActivity\"::smallint;");

            migrationBuilder.Sql(
                "ALTER TABLE mobile_device_motion_activity ALTER COLUMN \"Id\" TYPE smallint USING \"Id\"::smallint;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE mobile_gps_device_track_point ALTER COLUMN \"MotionActivity\" TYPE text USING \"MotionActivity\"::text;");

            migrationBuilder.Sql(
                "ALTER TABLE mobile_gps_device_map_state ALTER COLUMN \"MotionActivity\" TYPE text USING \"MotionActivity\"::text;");

            migrationBuilder.Sql(
                "ALTER TABLE mobile_device_motion_activity ALTER COLUMN \"Id\" TYPE text USING \"Id\"::text;");
        }
    }
}
