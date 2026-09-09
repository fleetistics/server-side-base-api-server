using mf.aiApi.mainDatabase.DatabaseContext;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace api_server.core
{
	/// <summary>
	/// Readiness probe: the process is only "ready" when the database answers.
	/// Wired to /health/ready; /health/live deliberately skips it (a DB outage
	/// should fail readiness, not get the process restarted).
	/// </summary>
	public sealed class DbReadyHealthCheck : IHealthCheck
	{
		private readonly MainDatabaseContext mDb;

		public DbReadyHealthCheck(MainDatabaseContext db)
		{
			mDb = db;
		}

		public async Task<HealthCheckResult> CheckHealthAsync(
			HealthCheckContext context, CancellationToken cancellationToken = default)
		{
			try
			{
				return await mDb.Database.CanConnectAsync(cancellationToken)
					? HealthCheckResult.Healthy("Database reachable.")
					: HealthCheckResult.Unhealthy("Database unreachable.");
			}
			catch (Exception ex)
			{
				return HealthCheckResult.Unhealthy("Database check failed.", ex);
			}
		}
	}
}
