using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Map;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class GpsDeviceMapStateConfiguration : IEntityTypeConfiguration<GpsDeviceMapState>
	{
		public void Configure(EntityTypeBuilder<GpsDeviceMapState> entity)
		{
			entity.ToTable("gps_device_map_state");
			entity.HasKey(e => new { e.GpsDeviceId });
			entity.Property(e => e.GpsDeviceId).ValueGeneratedNever();

			// The map reads a company's devices at once. CompanyId never changes on a position update, so
			// this index does not defeat HOT updates; Location/LastPositionAt/StatusId must stay unindexed.
			entity.HasIndex(e => e.CompanyId);

			// Sentinel = the default, so an explicit StatusId of 0 (Invisible) is inserted instead of being replaced by the default.
			entity.Property(e => e.StatusId).HasDefaultValue((short)GpsDeviceMapStatus.Visible).HasSentinel((short)GpsDeviceMapStatus.Visible);
			entity.Property(e => e.LocationAccuracyId).HasDefaultValue((short)GPSLocationAccuracy.Fine);

			// Rewritten every few seconds per moving device: leave room on the page for HOT updates.
			entity.HasStorageParameter("fillfactor", 75);
		}
	}
}
