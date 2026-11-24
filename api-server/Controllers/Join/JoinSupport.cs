using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace api_server.Controllers.Join
{
	/// <summary>Bound from the "Join" configuration section.</summary>
	public class JoinOptions
	{
		/// <summary>
		/// Where the portal lives, without a trailing slash (for example https://portal.example.com). The "accept invitation" link in the
		/// email starts with it. When it is empty the Origin header of the request that sends the invitation is used, which only suits development.
		/// </summary>
		public string? PortalBaseUrl { get; set; }

		/// <summary>How long an invitation to one person lasts, from when it is sent or renewed.</summary>
		public int DefaultInviteDays { get; set; } = 7;

		/// <summary>
		/// The name of the role a new company join code gives (a system role, or one of the company's). Only used when the code is first made;
		/// after that the admin's choice is kept. When no such role exists the company's default role is used.
		/// </summary>
		public string CompanyCodeRole { get; set; } = "Read-Only";

		/// <summary>Wrong codes one user may enter within <see cref="FailureWindowMinutes"/> before they are locked out for <see cref="LockoutMinutes"/>.</summary>
		public int MaxFailedAttempts { get; set; } = 5;
		public int FailureWindowMinutes { get; set; } = 15;
		public int LockoutMinutes { get; set; } = 15;
	}

	/// <summary>
	/// Makes and reads join codes. A code is 8 symbols from an alphabet without look-alikes, written XXXX-XXXX. It is stored as written
	/// (UserJoinInvitation.JoinCode), so <see cref="Format"/> turns what a person typed into the stored shape.
	/// </summary>
	public sealed class JoinCodes
	{
		private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

		public string Generate()
		{
			var chars = new char[9];
			for (var i = 0; i < 8; i++) chars[i < 4 ? i : i + 1] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
			chars[4] = '-';
			return new string(chars);
		}

		/// <summary>The code as typed, reduced to what counts: letters and digits, upper case. Null when it cannot be a code.</summary>
		public static string? Normalize(string? typed)
		{
			if (string.IsNullOrWhiteSpace(typed)) return null;
			var sb = new StringBuilder(8);
			foreach (var c in typed) if (char.IsAsciiLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
			return sb.Length == 8 ? sb.ToString() : null;
		}

		/// <summary>The code as stored: XXXX-XXXX. Null when the typed text cannot be a code.</summary>
		public static string? Format(string? typed)
		{
			var normalized = Normalize(typed);
			return normalized == null ? null : normalized[..4] + "-" + normalized[4..];
		}
	}

	/// <summary>
	/// Brake on guessing join codes, per signed-in user: after <see cref="JoinOptions.MaxFailedAttempts"/> wrong codes inside the window
	/// the user is refused for the lockout time, whatever they enter. In memory, so per server instance and cleared on restart.
	/// </summary>
	public sealed class JoinAttemptThrottle(IOptions<JoinOptions> options, TimeProvider time)
	{
		private sealed class Entry
		{
			public int Failures;
			public DateTime WindowStartUtc;
			public DateTime LockedUntilUtc;
		}

		private readonly ConcurrentDictionary<int, Entry> mEntries = new();

		public bool IsLocked(int userId)
		{
			if (options.Value.MaxFailedAttempts <= 0 || !mEntries.TryGetValue(userId, out var entry)) return false;
			lock (entry) return entry.LockedUntilUtc > time.GetUtcNow().UtcDateTime;
		}

		public void RecordFailure(int userId)
		{
			var o = options.Value;
			if (o.MaxFailedAttempts <= 0) return;
			var now = time.GetUtcNow().UtcDateTime;
			var entry = mEntries.GetOrAdd(userId, _ => new Entry { WindowStartUtc = now });
			lock (entry)
			{
				if (now - entry.WindowStartUtc > TimeSpan.FromMinutes(Math.Max(1, o.FailureWindowMinutes)))
				{
					entry.Failures = 0;
					entry.WindowStartUtc = now;
				}
				entry.Failures++;
				if (entry.Failures >= o.MaxFailedAttempts)
				{
					entry.LockedUntilUtc = now + TimeSpan.FromMinutes(Math.Max(1, o.LockoutMinutes));
					entry.Failures = 0;
					entry.WindowStartUtc = now;
				}
			}
		}

		public void Reset(int userId) => mEntries.TryRemove(userId, out _);
	}
}
