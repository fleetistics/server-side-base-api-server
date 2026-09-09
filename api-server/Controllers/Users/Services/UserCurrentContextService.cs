namespace api_server.Controllers.Users.Services
{
	public interface IUserCurrentContextService
	{
		Task<int> GetMyActiveTeamIdAsync(int userId, CancellationToken cancellationToken);
	}

	public sealed class UserCurrentContextService : IUserCurrentContextService
	{
		public Task<int> GetMyActiveTeamIdAsync(int userId, CancellationToken cancellationToken)
		{
			return Task.FromResult(7);
		}
	}
}
