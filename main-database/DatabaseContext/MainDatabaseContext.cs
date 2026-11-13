using db_model.Map;
using exs.dbContextCommons;
using exs.notifications_database;
using Microsoft.EntityFrameworkCore;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext : BaseDatabaseContext
	{
		public MainDatabaseContext(DbContextOptions options) : base(options) { }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// BaseDatabaseContext makes every LatestUpdate an optimistic-lock token. These two rows are rewritten by the feeders
			// (raw upserts) and the API at the same time, so a concurrent trigger update must not fail an EF save: the column stays
			// DB-generated (DEFAULT now() + trigger) but is not checked. Must run after the base call, which sets the token.
			modelBuilder.Entity<GpsDeviceMapState>().Property(e => e.LatestUpdate).IsConcurrencyToken(false);
			modelBuilder.Entity<GpsDeviceStateData>().Property(e => e.LatestUpdate).IsConcurrencyToken(false);
		}

		protected override void createModel(ModelBuilder modelBuilder)
		{
			NotificationsModelBuilder.CreateModel(modelBuilder);
			createUserModel(modelBuilder);
			createMediaModel(modelBuilder);
			createLogModel(modelBuilder);
			createTranslationModel(modelBuilder);
			createGpsModel(modelBuilder);
			createMapModel(modelBuilder);
			createFleetModel(modelBuilder);
			createTeamModel(modelBuilder);
			createMessageModel(modelBuilder);
			createCompaniesModel(modelBuilder);
			createAuthModel(modelBuilder);
		}
	}
}
