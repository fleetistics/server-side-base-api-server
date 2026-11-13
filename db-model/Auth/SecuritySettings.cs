using System.Text.Json;

namespace db_model.Auth
{
	/// <summary>
	/// The security settings document: serialized into <see cref="SettingsEntry.Value"/>.
	/// Property initializers are the defaults, so JSON written before a property was added still loads sane values.
	/// Never rename or repurpose a property; add a new one instead. Durations are plain numbers, 0 = never/off.
	/// </summary>
	public class SecuritySettings
	{
		public PasswordSettings Password { get; set; } = new();
		public TwoFactorSettings TwoFactor { get; set; } = new();
		public EmailDomainSettings EmailDomain { get; set; } = new();
		public SessionSettings Sessions { get; set; } = new();

		public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

		public static SecuritySettings FromJson(string json) => JsonSerializer.Deserialize<SecuritySettings>(json, JsonOptions) ?? new SecuritySettings();

		private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	}

	public class PasswordSettings
	{
		public int MinLength { get; set; } = 12;

		public bool RequireUpperAndLowerCase { get; set; } = true;
		public bool RequireNumber { get; set; } = true;
		public bool RequireSymbol { get; set; } = true;
		public bool DisallowNameOrEmail { get; set; } = true;
		public bool ScreenAgainstBreachedLists { get; set; } = true;

		/// <summary>Force rotation after this many days; 0 = never.</summary>
		public int ResetFrequencyDays { get; set; } = 90;
		/// <summary>Number of recent passwords that cannot be reused; 0 = no history check.</summary>
		public int HistoryCount { get; set; } = 5;
		/// <summary>Days before a user may change the password again; 0 = no minimum.</summary>
		public int MinAgeDays { get; set; } = 1;
	}

	public class TwoFactorSettings
	{
		public bool Required { get; set; } = true;
		public TwoFactorScope AppliesTo { get; set; } = TwoFactorScope.AllUsers;

		public bool AllowAuthenticatorApp { get; set; } = true;
		public bool AllowEmailOneTimeCode { get; set; } = false;
		public bool AllowBackupCodes { get; set; } = true;
		public bool AllowSmsMessage { get; set; } = true;
		public bool AllowSecurityKeyPasskey { get; set; } = true;

		/// <summary>Days a user has to enroll after first sign-in.</summary>
		public int EnrollmentGraceDays { get; set; } = 7;
		/// <summary>Days a trusted device skips the second factor; 0 = always ask.</summary>
		public int RememberDeviceDays { get; set; } = 30;
	}

	/// <summary>The domain list itself is not here: it is looked up by domain, so it belongs in its own indexed table.</summary>
	public class EmailDomainSettings
	{
		public bool DomainOnlyAccounts { get; set; } = true;
		public bool JustInTimeProvisioning { get; set; } = true;
		public bool ScimDirectorySync { get; set; } = false;
	}

	public class SessionSettings
	{
		/// <summary>Sign out after this many idle minutes; 0 = never.</summary>
		public int IdleTimeoutMinutes { get; set; } = 30;
		/// <summary>Force re-authentication after this many hours; 0 = no limit.</summary>
		public int MaxSessionHours { get; set; } = 12;
		/// <summary>Devices signed in at once per user; 0 = unlimited.</summary>
		public int ConcurrentSessions { get; set; } = 3;

		/// <summary>Failed attempts before the account locks; 0 = never lock.</summary>
		public int LockoutAttempts { get; set; } = 5;
		public int LockoutMinutes { get; set; } = 15;

		public bool StepUpReauthentication { get; set; } = true;
		public bool IpAllowlistEnabled { get; set; } = false;
		public bool GeoBlockingEnabled { get; set; } = true;
		public bool DeviceTrustForAdminRoles { get; set; } = true;

		public bool CaptchaOnRepeatedFailures { get; set; } = true;
		public int CaptchaAfterFailedAttempts { get; set; } = 3;
	}

	/// <summary>Stored as a number in the JSON: never renumber.</summary>
	public enum TwoFactorScope : short
	{
		AllUsers = 1,
		AdminsOnly = 2,
	}
}
