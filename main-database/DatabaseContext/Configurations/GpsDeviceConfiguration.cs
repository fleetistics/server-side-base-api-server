using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Gps;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	/// <summary>
	/// Shared by <see cref="MainDatabaseContext"/> and the feeders' own context (which maps this table as
	/// excluded from its migrations), so the table/column mapping has exactly one definition.
	/// </summary>
	public class GpsDeviceConfiguration : IEntityTypeConfiguration<GpsDevice>
	{
		public void Configure(EntityTypeBuilder<GpsDevice> entity)
		{
			entity.ToTable("gps_device");
			entity.HasKey(e => new { e.Id });
			entity.Property(e => e.Id).ValueGeneratedOnAdd();
			entity.HasIndex(e => e.CompanyId);
			// A phone logs in by its DeviceUID: a second login must find the device, never create another. Only for
			// Mobile: a Geotab serial is the hardware's and may legitimately appear under two accounts (a tracker
			// moved between customers), which would otherwise fail that account's asset sync.
			entity.HasIndex(e => new { e.ProviderId, e.SerialNumber }).IsUnique()
				.HasFilter($"\"ProviderId\" = {GpsDeviceProvider.Mobile}");
		}
	}
}
