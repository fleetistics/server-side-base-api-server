using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Map;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class GpsDeviceStateDataConfiguration : IEntityTypeConfiguration<GpsDeviceStateData>
	{
		public void Configure(EntityTypeBuilder<GpsDeviceStateData> entity)
		{
			entity.ToTable("gps_device_state_data");
			entity.HasKey(e => new { e.GpsDeviceId });
			entity.Property(e => e.GpsDeviceId).ValueGeneratedNever();
			entity.Property(e => e.Data).HasColumnType("jsonb");
			entity.HasStorageParameter("fillfactor", 75);
		}
	}
}
