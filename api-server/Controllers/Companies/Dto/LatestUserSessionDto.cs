using exs.modelCommons.AppStructure;
using exs.modelCommons.UserManagement;

namespace api_server.Companies
{
	/// <summary>A user's latest session; the session token data is deliberately not exposed.</summary>
	public class LatestUserSessionDto
	{
		public LatestUserSessionDto() { }

		public LatestUserSessionDto(UserSession model)
		{
			Id = model.Id;
			UserId = model.UserId;
			MobileGpsDeviceId = model.MobileGpsDeviceId;
			StatusId = model.StatusId;
			SessionSourceId = model.SessionSourceId;
			LatestUpdate = model.LatestUpdate;
			ClientInfo = new SessionClientInfo
			{
				AppUID = model.ClientInfo.AppUID,
				AppVersion = model.ClientInfo.AppVersion,
				DeviceUID = model.ClientInfo.DeviceUID,
				CodeVersion = model.ClientInfo.CodeVersion,
				PlatformId = model.ClientInfo.PlatformId,
				FCM_FID = model.ClientInfo.FCM_FID,
			};
		}

		public int Id { get; set; }
		public int UserId { get; set; }
		public int? MobileGpsDeviceId { get; set; }
		public short StatusId { get; set; }
		public short SessionSourceId { get; set; }
		public DateTime LatestUpdate { get; set; }
		public SessionClientInfo ClientInfo { get; set; } = new();
	}
}
