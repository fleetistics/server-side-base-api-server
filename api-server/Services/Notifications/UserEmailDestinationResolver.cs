using System.Net.Mail;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.notifications_model.Notifications;
using exs.notifications_service.Sender;
using Microsoft.EntityFrameworkCore;

namespace api_server.Services.Notifications
{
	/// <summary>
	/// Where this app's users are emailed: the address on the user record. Used when
	/// NotificationService:EmailTransport is "Sender"; which notifications are emailed at all is decided by
	/// NotificationService:Email, not here.
	///
	/// A user is mailable when they are active and their address is one bare, well-formed address. The address
	/// is typed in by the user and never verified, so a malformed one is expected and simply means "no address":
	/// the delivery log records that nothing was attempted, instead of the email sender refusing the message.
	/// </summary>
	public sealed class UserEmailDestinationResolver : IEmailDestinationResolver
	{
		public async Task<IReadOnlyList<EmailResolvedDestination>> ResolveAsync(IRepository repository, Notification notification, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken)
		{
			if (userIds.Count == 0) return [];

			var ids = userIds.ToList();
			var users = await repository.GetQueryable<User>(u => ids.Contains(u.Id) && u.StatusId == UserStatus.Active)
				.AsNoTracking()
				.Select(u => new { u.Id, u.Email, u.DisplayName, u.FullName })
				.ToListAsync(cancellationToken);

			return users
				.Select(u => (User: u, Address: u.Email?.Trim()))
				.Where(x => IsBareAddress(x.Address))
				.OrderBy(x => x.User.Id)
				.Select(x => new EmailResolvedDestination(x.User.Id, x.Address!, displayName(x.User.DisplayName, x.User.FullName), Ref: $"u:{x.User.Id}"))
				.ToList();
		}

		/// <summary>True for one plain address like sam@example.com, not "Sam &lt;sam@example.com&gt;" and not several.</summary>
		internal static bool IsBareAddress(string? address) =>
			!string.IsNullOrEmpty(address)
			&& MailAddress.TryCreate(address, out var parsed)
			&& string.Equals(parsed.Address, address, StringComparison.Ordinal)
			&& string.IsNullOrEmpty(parsed.DisplayName);

		/// <summary>The full name if there is one, else the display name. Control characters are dropped: a name with a line break would be refused by the email sender, and has no business in a header.</summary>
		private static string? displayName(string? displayName, string? fullName)
		{
			var name = !string.IsNullOrWhiteSpace(fullName) ? fullName : displayName;
			if (string.IsNullOrWhiteSpace(name)) return null;
			var clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
			return clean.Length == 0 ? null : clean.Length > 200 ? clean[..200] : clean;
		}
	}
}
