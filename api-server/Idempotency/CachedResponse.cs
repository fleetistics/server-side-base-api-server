namespace api_server.Idempotency
{
	public class CachedResponse
	{
		public int StatusCode { get; init; }
		public object? Body { get; init; }
	}
}
