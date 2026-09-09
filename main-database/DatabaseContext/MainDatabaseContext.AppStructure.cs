using Microsoft.EntityFrameworkCore;

using db_model.AppStructure;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createAppStructureModel(ModelBuilder modelBuilder)
		{
			// SessionClientInfo is a complex type embedded in UserSession.ClientInfo
			// (see createUserModel), not a standalone table — nothing to register here for it.
			modelBuilder.Entity<ClientDevicePlatform>(entity =>
			{
				entity.ToTable("client_device_platform");
				entity.HasKey(e => new { e.Id });
			});
		}
	}
}
