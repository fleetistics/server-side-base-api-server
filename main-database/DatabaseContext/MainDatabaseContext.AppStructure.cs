using Microsoft.EntityFrameworkCore;

using db_model.AppStructure;
using exs.modelCommons.AppStructure;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createAppStructureModel(ModelBuilder modelBuilder)
		{
			// SessionClientInfo is a complex type embedded in UserSession.ClientInfo
			// (see createUserModel), not a standalone table — nothing to register here for it.

			modelBuilder.Entity<ClientApplication>(entity =>
			{
				entity.ToTable("client_application");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<ApplicationPage>(entity =>
			{
				entity.ToTable("application_page");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<ApplicationMenu>(entity =>
			{
				entity.ToTable("application_menu");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<ApplicationMenuItem>(entity =>
			{
				entity.ToTable("application_menu_item");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
		}
	}
}
