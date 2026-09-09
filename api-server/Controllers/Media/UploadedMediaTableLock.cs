using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Media
{
	/// <summary>
	/// Shared by MediaStorageService and MediaInboundProcessor: both do a
	/// check-by-Guid-then-write against uploaded_media and need to block concurrent
	/// writers from racing between the check and the write.
	///
	/// Postgres has no manual UNLOCK TABLE — a table lock is held for the lifetime of
	/// the transaction that took it and is released automatically on COMMIT/ROLLBACK.
	/// The context is configured with a retrying execution strategy, which refuses a
	/// transaction started via plain BeginTransactionAsync ("user-initiated
	/// transactions" aren't retriable as a unit) — so the begin/lock/commit has to run
	/// as the body of Database.CreateExecutionStrategy().ExecuteAsync(...), with the
	/// caller's work passed in as a delegate rather than handed a transaction to manage.
	/// </summary>
	internal static class UploadedMediaTableLock
	{
		private const string cTableName = "uploaded_media";

		public static async Task<T> ExecuteAsync<T>(DbContext context, Func<CancellationToken, Task<T>> body, CancellationToken cancellationToken)
		{
			var strategy = context.Database.CreateExecutionStrategy();
			return await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
				try
				{
					// SHARE ROW EXCLUSIVE conflicts with itself (and with row-level writers) but
					// still allows plain reads, so concurrent check-then-write callers serialize
					// against each other without blocking unrelated SELECTs.
					await context.Database.ExecuteSqlRawAsync($"LOCK TABLE {cTableName} IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
					var result = await body(cancellationToken);
					await transaction.CommitAsync(cancellationToken);
					return result;
				}
				catch
				{
					await transaction.RollbackAsync(CancellationToken.None);
					throw;
				}
			});
		}
	}
}
