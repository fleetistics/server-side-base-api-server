using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext : DbContext
	{
		public MainDatabaseContext(DbContextOptions options) : base(options) { }
	

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			createUserModel(modelBuilder);
			createMediaModel(modelBuilder);
			createLogModel(modelBuilder);
			createTranslationModel(modelBuilder);
			createGpsModel(modelBuilder);
			createAppStructureModel(modelBuilder);
			createMapModel(modelBuilder);
			createTeamModel(modelBuilder);

			// Every optimistic-lock column: DB stamps it via DEFAULT on insert and the
			// update_latestupdate_column trigger (LatestUpdateTriggerSqlGenerator) on update, so EF
			// never writes a value itself and must re-read it after every write.
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				var property = entityType.FindProperty("LatestUpdate");
				if (property != null && property.ClrType == typeof(DateTime))
				{
					property.SetDefaultValueSql("now()");
					property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
					property.IsConcurrencyToken = true;
				}
			}
		}
	}
}
