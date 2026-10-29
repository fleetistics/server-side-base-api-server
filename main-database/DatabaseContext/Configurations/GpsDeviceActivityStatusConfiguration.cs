using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Map;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class GpsDeviceActivityStatusConfiguration : IEntityTypeConfiguration<GpsDeviceActivityStatus>
	{
		public void Configure(EntityTypeBuilder<GpsDeviceActivityStatus> entity)
		{
			entity.ToTable("gps_device_activity_status");
			entity.HasKey(e => new { e.Id });
			entity.Property(e => e.Id).ValueGeneratedNever();
		}
	}
}
