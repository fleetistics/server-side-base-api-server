using Microsoft.EntityFrameworkCore;

using mf.aiApi.mainDatabase.DatabaseContext.Configurations;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		// Company and the map state of GPS devices fed by the server-side-feeders. The feeders' own context
		// applies the same configurations (as excluded from its migrations); this context owns the tables.
		private void createFleetModel(ModelBuilder modelBuilder)
		{
			modelBuilder.ApplyConfiguration(new CompanyConfiguration());
			modelBuilder.ApplyConfiguration(new GpsDeviceMapStatusConfiguration());
			modelBuilder.ApplyConfiguration(new GpsDeviceMapStateConfiguration());
			modelBuilder.ApplyConfiguration(new GpsDeviceStateDataConfiguration());
		}
	}
}
