using exs.dbContextCommons;
using exs.notifications_database;
using Microsoft.EntityFrameworkCore;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext : BaseDatabaseContext
	{
		public MainDatabaseContext(DbContextOptions options) : base(options) { }

		protected override void createModel(ModelBuilder modelBuilder)
		{
			NotificationsModelBuilder.CreateModel(modelBuilder);
			createUserModel(modelBuilder);
			createMediaModel(modelBuilder);
			createLogModel(modelBuilder);
			createTranslationModel(modelBuilder);
			createGpsModel(modelBuilder);
			createMapModel(modelBuilder);
			createTeamModel(modelBuilder);
		}
	}
}
