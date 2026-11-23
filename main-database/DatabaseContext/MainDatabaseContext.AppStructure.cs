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
			modelBuilder.Entity<Role2MenuItem>(entity =>
			{
				entity.ToTable("role2menu_item");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				// One row per role and item for the defaults (CompanyId null) and one per role, item and company otherwise. Two filtered
				// indexes because a null in a unique index counts as different from another null.
				entity.HasIndex(e => new { e.PermissionRoleId, e.ApplicationMenuItemId })
					.IsUnique()
					.HasFilter("\"CompanyId\" IS NULL")
					.HasDatabaseName("UX_role2menu_item_default");
				entity.HasIndex(e => new { e.PermissionRoleId, e.ApplicationMenuItemId, e.CompanyId })
					.IsUnique()
					.HasFilter("\"CompanyId\" IS NOT NULL")
					.HasDatabaseName("UX_role2menu_item_company");
			});
		}
	}
}
