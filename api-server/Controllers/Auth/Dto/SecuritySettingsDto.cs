using db_model.Auth;

namespace api_server.Auth.Dto
{
	public class SecuritySettingsDto
	{
		public SecuritySettingsDto() { }

		public SecuritySettingsDto(int? companyId, SecuritySettings settings)
		{
			CompanyId = companyId;
			Settings = settings;
		}

		/// <summary>The company these settings belong to; null when the system-wide default is returned.</summary>
		public int? CompanyId { get; set; }

		public bool IsSystemDefault => CompanyId == null;

		public SecuritySettings Settings { get; set; } = new();
	}
}
