using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Gps;
using db_model.Map;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class GpsDeviceHourlyTotalConfiguration : IEntityTypeConfiguration<GpsDeviceHourlyTotal>
	{
		public void Configure(EntityTypeBuilder<GpsDeviceHourlyTotal> entity)
		{
			entity.ToTable("gps_device_hourly_total");
			// One row per device and hour; a range of hours of one device is a key range.
			entity.HasKey(e => new { e.GpsDeviceId, e.HourUtc });
			// For clearing old hours (retention) and for "everything of this hour".
			entity.HasIndex(e => e.HourUtc);
			// Written only by the feeders, with plain SQL; a deleted device takes its history with it.
			entity.HasOne<GpsDevice>().WithMany().HasForeignKey(e => e.GpsDeviceId).OnDelete(DeleteBehavior.Cascade);
		}
	}
}
