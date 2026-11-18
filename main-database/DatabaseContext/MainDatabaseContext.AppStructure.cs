using Microsoft.EntityFrameworkCore;

using db_model.AppStructure;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createAppStructureModel(ModelBuilder modelBuilder)
		{
			// The tables below (application_menu, application_menu_item, application_page, role2menu_item) already exist in the database (created by hand). The model only declares tables, keys and
			// required columns; foreign keys and indexes are deliberately not modelled because the existing DDL is not known.
			// Note: SessionClientInfo is a complex type embedded in UserSession.ClientInfo (see createUserModel), not a table.
			modelBuilder.Entity<ApplicationMenu>(entity =>
			{
				entity.ToTable("application_menu");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<ApplicationMenuItem>(entity =>
			{
				entity.ToTable("application_menu_item");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.Title).IsRequired();
			});
			modelBuilder.Entity<ApplicationPage>(entity =>
			{
				entity.ToTable("application_page");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.Path).IsRequired();
				entity.Property(e => e.CodeClassName).IsRequired();
			});
			modelBuilder.Entity<Role2MenuItem>(entity =>
			{
				entity.ToTable("role2menu_item");
				entity.HasKey(e => new { e.PermissionRoleId, e.ApplicationMenuItemId });
			});
		}
	}
}
