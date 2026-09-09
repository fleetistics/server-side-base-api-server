using Microsoft.EntityFrameworkCore;

using db_model.Gps;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createGpsModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<GpsDevice>(entity =>
			{
				entity.ToTable("gps_device");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
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
			modelBuilder.Entity<MobileGpsDeviceProvider>(entity =>
			{
				entity.ToTable("mobile_gps_device_provider");
				entity.HasKey(e => new { e.Id });
			});
			modelBuilder.Entity<MobileGpsDevice>(entity =>
			{
				entity.ToTable("mobile_gps_device");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
		}
	}
}
