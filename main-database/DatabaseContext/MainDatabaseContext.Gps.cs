using Microsoft.EntityFrameworkCore;

using db_model.Gps;
using mf.aiApi.mainDatabase.DatabaseContext.Configurations;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createGpsModel(ModelBuilder modelBuilder)
		{
			modelBuilder.ApplyConfiguration(new GpsDeviceConfiguration());
			modelBuilder.Entity<GpsDeviceProvider>(entity =>
			{
				entity.ToTable("gps_device_provider");
				entity.HasKey(e => new { e.Id });
			});
			modelBuilder.Entity<GpsDeviceStatus>(entity =>
			{
				entity.ToTable("gps_device_status");
				entity.HasKey(e => new { e.Id });
			});
			modelBuilder.Entity<GpsDeviceType>(entity =>
			{
				entity.ToTable("gps_device_type");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasIndex(e => new { e.ProviderId, e.Name }).IsUnique();
				entity.HasOne<GpsDeviceType>().WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
				// Unlike the other (hand-filled) ref tables, the code depends on these two ids and a foreign key
				// guards them, so they are seeded by the model (also under EnsureCreated in the tests).
				var seeded = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
				entity.HasData(
					new GpsDeviceType { Id = GpsDeviceType.MobileAndroid, Name = "Android", ProviderId = GpsDeviceProvider.Mobile, LatestUpdate = seeded },
					new GpsDeviceType { Id = GpsDeviceType.MobileIos, Name = "iOS", ProviderId = GpsDeviceProvider.Mobile, LatestUpdate = seeded });
			});
			// Not in GpsDeviceConfiguration: the feeders' context shares that one and does not map the type table.
			modelBuilder.Entity<GpsDevice>()
				.HasOne<GpsDeviceType>().WithMany().HasForeignKey(e => e.TypeId).OnDelete(DeleteBehavior.Restrict);
			// The mobile_gps_device and mobile_gps_device_provider tables are no longer mapped: their rows were
			// copied into gps_device (same ids). The tables stay in the database for one release as a safety net.
		}
	}
}
