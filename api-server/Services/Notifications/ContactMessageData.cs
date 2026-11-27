using exs.notifications_service.Templates;

namespace api_server.Services.Notifications
{
	/// <summary>
	/// The data of the "contact-message" email (Contacts: the Message button of a contact): {{ RecipientName }}, {{ SenderName }},
	/// {{ SenderCompanyName }}, {{ Subject }}, {{ Body }}, named exactly as the properties below. The body is plain text the sender typed; the
	/// HTML of the template escapes it and keeps its line breaks with white-space:pre-wrap. The wording lives in the notification_template row
	/// with the key below (a starting English one is in custom-sql/insert-notification-template-contact-message.sql).
	/// </summary>
	public sealed class ContactMessageData : ITemplateModel
	{
		public static string TemplateKey => "contact-message";

		/// <summary>How the email greets the person.</summary>
		public required string RecipientName { get; init; }

		/// <summary>The user who wrote the message; replies go to their address.</summary>
		public required string SenderName { get; init; }

		/// <summary>The company the sender works for.</summary>
		public required string SenderCompanyName { get; init; }

		/// <summary>The subject line, also used as the title in the text.</summary>
		public required string Subject { get; init; }

		/// <summary>What the sender typed (plain text).</summary>
		public required string Body { get; init; }
	}
}
