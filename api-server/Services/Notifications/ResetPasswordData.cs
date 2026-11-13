using exs.notifications_service.Templates;

namespace api_server.Services.Notifications
{
	/// <summary>
	/// The data of the "password-reset" email: what its template ({{ user_name }}, {{ reset_link }}, {{ expires_at_utc }}) can use.
	/// Filled by whoever sends it and handed to INotificationSender.CreateEmailFromTemplateAsync; the wording lives in the
	/// notification_template row with the key below, one per language.
	///
	/// The link carries the reset key, which is a credential until it is used or expires, so a template must wrap it in
	/// <c>&lt;secure&gt;...&lt;/secure&gt;</c>: the email sender removes the tags before sending and keeps the message with the link masked.
	/// </summary>
	public sealed class ResetPasswordData : ITemplateModel
	{
		public static string TemplateKey => "password-reset";

		/// <summary>How the email greets the user.</summary>
		public required string UserName { get; init; }

		/// <summary>The full link that opens the reset page for this key.</summary>
		public required string ResetLink { get; init; }

		/// <summary>When the key stops working.</summary>
		public required DateTime ExpiresAtUtc { get; init; }
	}
}
