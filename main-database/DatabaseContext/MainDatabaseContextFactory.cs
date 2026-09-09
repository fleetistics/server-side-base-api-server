using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	// Used only by `dotnet ef` at design time (migrations add/update). Bypasses the
	// api-server host entirely, since its config loading is relative to the built
	// output folder and not something the EF tooling can boot on its own.
	public sealed class MainDatabaseContextFactory : IDesignTimeDbContextFactory<MainDatabaseContext>
	{
		public MainDatabaseContext CreateDbContext(string[] args)
		{
			var connectionString =
				Environment.GetEnvironmentVariable("ConnectionStrings__MainDatabase")
				?? "Host=74.115.172.125;Database=base-dev-db;Username=base-dev-user;Password=base$dev";

			var options = new DbContextOptionsBuilder<MainDatabaseContext>()
				.UseNpgsql(connectionString, opts => opts.UseNetTopologySuite())
				.ReplaceService<IMigrationsSqlGenerator, LatestUpdateTriggerSqlGenerator>()
				.ReplaceService<IMigrationsModelDiffer, NoDropMigrationsModelDiffer>()
				.Options;

			return new MainDatabaseContext(options);
		}
	}
}
