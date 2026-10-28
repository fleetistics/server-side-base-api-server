using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Map;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class GpsDeviceMapStatusConfiguration : IEntityTypeConfiguration<GpsDeviceMapStatus>
	{
		public void Configure(EntityTypeBuilder<GpsDeviceMapStatus> entity)
		{
			entity.ToTable("gps_device_map_status");
			entity.HasKey(e => new { e.Id });
			entity.Property(e => e.Id).ValueGeneratedNever();
		}
	}
}
