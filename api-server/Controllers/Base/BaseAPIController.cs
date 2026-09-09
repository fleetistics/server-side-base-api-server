using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Base
{
	[Produces("application/json")]
	[ApiController]
	[Route("api/[controller]/[action]")]
	[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
	public class BaseAPIController : ControllerBase
	{
		protected BaseAPIController() { }

		// RFC 7807 ProblemDetails, consistent with the global exception handler.
		// The traceId extension is added by AddProblemDetails() in Program.cs.
		protected ObjectResult ErrorResult() => Problem(statusCode: 500);
		protected ObjectResult ErrorResult(string message) => Problem(detail: message, statusCode: 500);
	}
}
