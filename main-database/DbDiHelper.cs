using exs.Database.Commons.Impl;
using exs.Database.Commons.Interfaces;
using exs.databaseCommons.Impl;
using exs.databaseCommons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using mf.aiApi.mainDatabase.DatabaseContext;

namespace mf.aiApi.mainDatabase.Config
{
	public static class DbDiHelper
	{
		public static IServiceCollection AddDbServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddSingleton<IRepositoryFactory, EntityFrameworkRepositoryFactory>()
				.AddScoped<IRepository, EntityFrameworkRepository<MainDatabaseContext>>()
				.AddTransient<IScopedRepository, ScopedEntityFrameworkRepository<MainDatabaseContext>>(sp =>
					new ScopedEntityFrameworkRepository<MainDatabaseContext>(sp.GetRequiredService<MainDatabaseContext>(), sp.CreateScope()))
				.AddDbContext<MainDatabaseContext>(options =>
					options.UseNpgsql(configuration["ConnectionStrings:MainDatabase"], opts =>
					{
						opts.UseNetTopologySuite()
							.EnableRetryOnFailure(
								maxRetryCount: 5,
								maxRetryDelay: TimeSpan.FromSeconds(10),
								errorCodesToAdd: null)
							.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

					})
					.ReplaceService<IMigrationsSqlGenerator, LatestUpdateTriggerSqlGenerator>()
					.ReplaceService<IMigrationsModelDiffer, NoDropMigrationsModelDiffer>(),
					ServiceLifetime.Transient);
			return services;
		}
	}
}
