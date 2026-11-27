using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Contacts.Dto
{
	/// <summary>A message to a contact, sent by email.</summary>
	public class SendContactMessageDto
	{
		/// <summary>Optional; "Message from {sender}" when left empty.</summary>
		[MaxLength(200)] public string? Subject { get; set; }

		/// <summary>Plain text, required.</summary>
		[Required, MaxLength(5000)] public string Body { get; set; } = default!;
	}

	/// <summary>What came of sending a message: the email is queued and an Email activity is logged.</summary>
	public class ContactMessageResultDto
	{
		/// <summary>The Email activity made for the message.</summary>
		public int ActivityId { get; set; }
	}
}
