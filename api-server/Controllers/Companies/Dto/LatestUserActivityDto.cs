using exs.modelCommons.AppStructure;
using exs.modelCommons.UserManagement;

namespace api_server.Companies
{
	/// <summary>A user's latest session; the session token data is deliberately not exposed.</summary>
	public class LatestUserActivityDto
    {
		public LatestUserActivityDto() { }

		public LatestUserActivityDto(UserSession model)
		{
			UserId = model.UserId;
			LatestUpdate = model.LatestUpdate;
		}

		public int UserId { get; set; }
		public DateTime LatestUpdate { get; set; }
	}
}
