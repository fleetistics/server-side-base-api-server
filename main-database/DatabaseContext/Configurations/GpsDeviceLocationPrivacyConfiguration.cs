using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Gps;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class GpsDeviceLocationPrivacyConfiguration : IEntityTypeConfiguration<GpsDeviceLocationPrivacy>
	{
		public void Configure(EntityTypeBuilder<GpsDeviceLocationPrivacy> entity)
		{
			entity.ToTable("gps_device_location_privacy");
			// Shared primary key with the device, like user_location_privacy with user.
			entity.HasKey(e => e.GpsDeviceId);
			entity.Property(e => e.GpsDeviceId).ValueGeneratedNever();
			entity.HasOne<GpsDevice>().WithOne().HasForeignKey<GpsDeviceLocationPrivacy>(e => e.GpsDeviceId).OnDelete(DeleteBehavior.Cascade);
		}
	}
}
