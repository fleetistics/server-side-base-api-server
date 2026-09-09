using Microsoft.AspNetCore.Mvc;

namespace api_server.Idempotency
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class IdempotentAttribute : TypeFilterAttribute<IdempotencyFilter>
	{
	}
}
