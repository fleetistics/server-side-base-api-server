using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace api_server.core
{
	/// <summary>
	/// Last-resort handler: any exception that escapes a controller becomes an
	/// RFC 7807 ProblemDetails response instead of an empty 500. The traceId lets a
	/// support engineer (or the SPA's flight recorder) jump straight to the matching
	/// trace and log lines in the telemetry backend.
	/// </summary>
	public sealed class GlobalExceptionHandler : IExceptionHandler
	{
		private readonly ILogger<GlobalExceptionHandler> mLogger;
		private readonly IHostEnvironment mEnvironment;

		public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
		{
			mLogger = logger;
			mEnvironment = environment;
		}

		public async ValueTask<bool> TryHandleAsync(
			HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
		{
			mLogger.LogError(exception, "Unhandled exception for {Method} {Path}",
				httpContext.Request.Method, httpContext.Request.Path);

			Activity.Current?.AddException(exception);
			Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

			var problem = new ProblemDetails
			{
				Status = StatusCodes.Status500InternalServerError,
				Title = "An unexpected error occurred.",
				// Exception details are for the telemetry backend, not for API clients.
				Detail = mEnvironment.IsDevelopment() ? exception.ToString() : null,
			};
			problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

			httpContext.Response.StatusCode = problem.Status.Value;
			await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
			return true;
		}
	}
}
