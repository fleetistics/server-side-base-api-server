using Microsoft.EntityFrameworkCore;

using db_model.Map;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createMapModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<GPSLocationAccuracy>(entity =>
			{
				entity.ToTable("gps_location_accuracy");
				entity.HasKey(e => new { e.Id });
			});
			modelBuilder.Entity<MobileDeviceMotionActivity>(entity =>
			{
				entity.ToTable("mobile_device_motion_activity");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
			modelBuilder.Entity<MobileGpsDeviceMapState>(entity =>
			{
				entity.ToTable("mobile_gps_device_map_state");
				entity.HasKey(e => new { e.MobileGpsDeviceId });
				entity.Property(e => e.MobileGpsDeviceId).ValueGeneratedNever();
			});
			modelBuilder.Entity<MobileGpsDeviceTrackPoint>(entity =>
			{
				entity.ToTable("mobile_gps_device_track_point");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
		}
	}
}
