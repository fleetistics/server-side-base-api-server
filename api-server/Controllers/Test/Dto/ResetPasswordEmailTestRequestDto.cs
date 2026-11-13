using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Test.Dto
{
	/// <summary>Body of POST /api/test/email/reset-password. Everything but <see cref="To"/> is optional.</summary>
	public class ResetPasswordEmailTestRequestDto
	{
		/// <summary>The recipient: one bare address such as sam@example.com.</summary>
		[Required]
		public string To { get; set; } = "";

		/// <summary>What the email calls the user ({{ user_name }}) and the recipient's display name. Default: "Test User".</summary>
		public string? UserName { get; set; }

		/// <summary>The language of the template wanted, such as "uk". Default, or a language with no template: the default language.</summary>
		public string? Language { get; set; }

		/// <summary>The link put in the email ({{ reset_link }}). Default: a made-up one that opens nothing, with a random key in it.</summary>
		public string? ResetLink { get; set; }

		/// <summary>How long from now the made-up key is said to be valid ({{ expires_at_utc }}). Default 60.</summary>
		[Range(1, 1440)]
		public int? ExpiresInMinutes { get; set; }

		/// <summary>The sender account alias to send as; default: the email sender's DefaultAccount for this host.</summary>
		public string? Account { get; set; }
	}
}
