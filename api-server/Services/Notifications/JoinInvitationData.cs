using exs.notifications_service.Templates;

namespace api_server.Services.Notifications
{
	/// <summary>
	/// The data of the "join-invitation" email: what its template ({{ InviteeName }}, {{ CompanyName }}, {{ InviterName }},
	/// {{ JoinCode }}, {{ AcceptLink }}, {{ Message }}, {{ ExpiresAtUtc }}, named exactly as the properties below) can use. Filled by the
	/// company join service and handed to INotificationSender.CreateEmailFromTemplateAsync; the wording lives in the notification_template
	/// row with the key below, one per language (a starting English one is in custom-sql/insert-notification-template-join-invitation.sql).
	///
	/// The code is a credential until it is used or expires, so a template must wrap it in <c>&lt;secure&gt;...&lt;/secure&gt;</c>: the email
	/// sender removes the tags before sending and keeps the message with the code masked. The link only opens the page where the code is
	/// entered; it carries no secret, so it needs no tags.
	/// </summary>
	public sealed class JoinInvitationData : ITemplateModel
	{
		public static string TemplateKey => "join-invitation";

		/// <summary>How the email greets the person: the name the inviter typed, else the part of the address before the @.</summary>
		public required string InviteeName { get; init; }

		/// <summary>The company that invites.</summary>
		public required string CompanyName { get; init; }

		/// <summary>Who in the company sent the invitation.</summary>
		public required string InviterName { get; init; }

		/// <summary>The one-time code the invitee types on the accept page, like K7QM-4XPD.</summary>
		public required string JoinCode { get; init; }

		/// <summary>The full link of the accept page for this invitation.</summary>
		public required string AcceptLink { get; init; }

		/// <summary>The inviter's personal note; null when there is none, so a template should test it ({% if Message %}).</summary>
		public string? Message { get; init; }

		/// <summary>When the code stops working.</summary>
		public required DateTime ExpiresAtUtc { get; init; }
	}
}
