using db_model.Log;
using mf.aiApi.mainDatabase.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Log
{
	public sealed class ClientLogRetentionOptions
	{
		/// <summary>Uploaded client logs older than this are deleted. Config key: ClientLog:RetentionDays.</summary>
		public int RetentionDays { get; set; } = 90;
	}

	/// <summary>
	/// Deletes client_log_record rows past retention, once at startup and then daily.
	/// Doubles as the template's example of a scoped-work BackgroundService: the
	/// singleton service creates a scope per run to use scoped/transient dependencies.
	/// </summary>
	public sealed class ClientLogRetentionService : BackgroundService
	{
		private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

		private readonly IServiceScopeFactory mScopeFactory;
		private readonly ClientLogRetentionOptions mOptions;
		private readonly ILogger<ClientLogRetentionService> mLogger;

		public ClientLogRetentionService(
			IServiceScopeFactory scopeFactory,
			Microsoft.Extensions.Options.IOptions<ClientLogRetentionOptions> options,
			ILogger<ClientLogRetentionService> logger)
		{
			mScopeFactory = scopeFactory;
			mOptions = options.Value;
			mLogger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using var scope = mScopeFactory.CreateScope();
					var db = scope.ServiceProvider.GetRequiredService<MainDatabaseContext>();

					var cutoff = DateTime.UtcNow.AddDays(-mOptions.RetentionDays);
					var deleted = await db.Set<ClientLogRecord>()
						.Where(r => r.LatestUpdate < cutoff)
						.ExecuteDeleteAsync(stoppingToken);

					if (deleted > 0)
					{
						mLogger.LogInformation(
							"ClientInfo-log retention: deleted {Count} records older than {Cutoff:u}", deleted, cutoff);
					}
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					// Retention is housekeeping — a failed run logs and retries next cycle,
					// it never takes the host down.
					mLogger.LogError(ex, "ClientInfo-log retention run failed");
				}

				await Task.Delay(RunInterval, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
			}
		}
	}
}
