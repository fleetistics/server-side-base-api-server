using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;

namespace api_server.Idempotency
{
	public class IdempotencyFilter : IAsyncActionFilter
	{
		private const string HeaderName = "Idempotency-Key";

		private readonly IdempotencyStore mStore;
		private readonly ILogger<IdempotencyFilter> mLogger;

		public IdempotencyFilter(IdempotencyStore store, ILogger<IdempotencyFilter> logger)
		{
			mStore = store;
			mLogger = logger;
		}

		public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
				|| string.IsNullOrWhiteSpace(keyValues))
			{
				context.Result = new BadRequestObjectResult(new { message = $"Missing required header: {HeaderName}." });
				return;
			}

			var rawKey = keyValues.ToString().Trim();
			if (!Guid.TryParse(rawKey, out _))
			{
				context.Result = new BadRequestObjectResult(new { message = $"{HeaderName} must be a valid GUID." });
				return;
			}

			// Scope key per user to prevent cross-user collisions
			var userId = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous";
			var cacheKey = $"{userId}:{rawKey}";

			if (mStore.TryGetResponse(cacheKey, out var cached))
			{
				mLogger.LogInformation("Idempotency cache hit for key {Key}", cacheKey);
				context.Result = new ObjectResult(cached!.Body) { StatusCode = cached.StatusCode };
				return;
			}

			var lockHandle = mStore.GetLock(cacheKey);
			await lockHandle.WaitAsync(context.HttpContext.RequestAborted);
			try
			{
				// Re-check after acquiring lock (another request may have just completed)
				if (mStore.TryGetResponse(cacheKey, out cached))
				{
					mLogger.LogInformation("Idempotency cache hit (post-lock) for key {Key}", cacheKey);
					context.Result = new ObjectResult(cached!.Body) { StatusCode = cached.StatusCode };
					return;
				}

				var executed = await next();

				if (executed.Result is ObjectResult objectResult && objectResult.StatusCode < 500)
				{
					mStore.StoreResponse(cacheKey, new CachedResponse
					{
						StatusCode = objectResult.StatusCode ?? StatusCodes.Status200OK,
						Body = objectResult.Value,
					});
				}
			}
			finally
			{
				mStore.ReleaseLock(cacheKey);
			}
		}
	}
}
