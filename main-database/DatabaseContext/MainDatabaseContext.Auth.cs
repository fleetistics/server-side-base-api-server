using Microsoft.EntityFrameworkCore;

using db_model.Auth;
using db_model.Companies;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createAuthModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<SettingsEntry>(entity =>
			{
				entity.ToTable("settings_entry");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.Value).HasColumnType("jsonb").IsRequired();
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);

				// At most one row per company. The single system row (CompanyId null) cannot be covered by this index,
				// because a unique index treats NULLs as distinct: that one is a hand-written index in the migration.
				entity.HasIndex(e => e.CompanyId).IsUnique().HasFilter("\"CompanyId\" IS NOT NULL");

				// Initial system-wide row: the defaults of SecuritySettings (the values of the portal's security page).
				entity.HasData(new SettingsEntry
				{
					Id = SystemSecuritySettingsId,
					CompanyId = null,
					Value = new SecuritySettings().ToJson(),
					LatestUpdate = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc)
				});
			});
		}

		private const int SystemSecuritySettingsId = 1;
	}
}
