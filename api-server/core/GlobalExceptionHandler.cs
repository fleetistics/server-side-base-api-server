using System.Diagnostics;
using exs.Commons;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace api_server.core
{
	/// <summary>
	/// Last-resort handler: any exception that escapes a controller becomes an
	/// RFC 7807 ProblemDetails response instead of an empty 500. The traceId lets a
	/// support engineer (or the SPA's flight recorder) jump straight to the matching
	/// trace and log lines in the telemetry backend.
	///
	/// Two exception types get a client-safe Title instead of the generic message:
	/// throw <see cref="TUserMessageException"/>/<see cref="TNotAuthorizedException"/> from
	/// anywhere below a controller action (service, repository, etc.) and its Message reaches
	/// the client as-is - so only put text there that's fine for an end user to read. Anything
	/// else is an unexpected failure: the client gets a generic message and the real details
	/// (exception.ToString(), with stack trace) go to the server log/telemetry only.
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
			ProblemDetails problem;

			switch (exception)
			{
				case TUserMessageException userMessage:
					// Expected/business-rule outcome, not a bug - Information, and no stack trace noise.
					mLogger.LogInformation("User-facing error for {Method} {Path}: {Message}",
						httpContext.Request.Method, httpContext.Request.Path, userMessage.Message);
					problem = new ProblemDetails
					{
						Status = StatusCodes.Status400BadRequest,
						Title = userMessage.Message,
					};
					break;

				case TNotAuthorizedException notAuthorized:
					mLogger.LogInformation("Not authorized for {Method} {Path}: {Message}",
						httpContext.Request.Method, httpContext.Request.Path, notAuthorized.Message);
					problem = new ProblemDetails
					{
						Status = StatusCodes.Status403Forbidden,
						Title = string.IsNullOrEmpty(notAuthorized.Message) ? "Not authorized." : notAuthorized.Message,
					};
					break;

				default:
					mLogger.LogError(exception, "Unhandled exception for {Method} {Path}",
						httpContext.Request.Method, httpContext.Request.Path);

					Activity.Current?.AddException(exception);
					Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

					problem = new ProblemDetails
					{
						Status = StatusCodes.Status500InternalServerError,
						Title = "An unexpected error occurred.",
						// Exception details are for the telemetry backend, not for API clients.
						Detail = mEnvironment.IsDevelopment() ? exception.ToString() : null,
					};
					break;
			}

			problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

			httpContext.Response.StatusCode = problem.Status.Value;
			await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
			return true;
		}
	}
}
