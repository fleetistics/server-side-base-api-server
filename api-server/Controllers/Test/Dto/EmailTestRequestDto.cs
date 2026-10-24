using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Test.Dto
{
	/// <summary>Body of POST /api/test/email. Everything but <see cref="To"/> is optional.</summary>
	public class EmailTestRequestDto
	{
		/// <summary>The recipient: one bare address such as sam@example.com.</summary>
		[Required]
		public string To { get; set; } = "";

		public string? ToName { get; set; }

		/// <summary>Default: "Test email " and the UTC time.</summary>
		public string? Subject { get; set; }

		/// <summary>Default, when neither Text nor Html is given: a fixed sentence saying where the mail came from.</summary>
		public string? Text { get; set; }

		public string? Html { get; set; }

		public string? ReplyTo { get; set; }

		/// <summary>"normal" or "high".</summary>
		public string? Priority { get; set; }

		/// <summary>The sender account alias to send as; default: the email sender's DefaultAccount for this host.</summary>
		public string? Account { get; set; }
	}
}
