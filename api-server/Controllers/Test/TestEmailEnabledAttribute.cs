using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api_server.Controllers.Test
{
	/// <summary>
	/// Makes the endpoints it is put on answer 404 unless the configuration key <c>TestEmail:Enabled</c> is
	/// <c>true</c> (default: false). The email test endpoints let any logged-in user send mail from the company's SMTP
	/// account to any address, so they must be off everywhere except a dev machine's own local config
	/// (<c>api-server/local_configs/local_server.json</c>, which is not committed). The key is read on every request,
	/// and an unset key means off.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public sealed class TestEmailEnabledAttribute : Attribute, IResourceFilter
	{
		public const string ConfigKey = "TestEmail:Enabled";

		public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(ConfigKey);

		public void OnResourceExecuting(ResourceExecutingContext context)
		{
			var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
			if (IsEnabled(configuration)) return;

			// Same shape as every other error of this host; nothing in it says that the endpoint exists.
			context.Result = new ObjectResult(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Not Found" })
			{
				StatusCode = StatusCodes.Status404NotFound,
			};
		}

		public void OnResourceExecuted(ResourceExecutedContext context) { }
	}
}
