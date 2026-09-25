using api_server.core;

namespace api_server.Controllers.Users.Dto
{
	public class UserSettingsPutDto
	{
		public string Name { get; set; }

		/// <summary>A null Value marks this tier reset - falls through to the next broader tier.</summary>
		[MeaningfulNull]
		public string? Value { get; set; }

		/// <summary>Null scopes the setting broadly (all client applications), not "unspecified".</summary>
		[MeaningfulNull]
		public short? ClientApplicationId { get; set; }

		/// <summary>Null scopes the setting broadly (all device platforms), not "unspecified".</summary>
		[MeaningfulNull]
		public short? ClientDevicePlatformId { get; set; }
	}
}
