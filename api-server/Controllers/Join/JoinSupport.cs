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

		/// <summary>How long a personal invitation lasts when the request does not say.</summary>
		public int DefaultInviteDays { get; set; } = 7;

		/// <summary>The longest an invitation may last.</summary>
		public int MaxInviteDays { get; set; } = 60;

		/// <summary>How long the company join code lasts before the page makes a new one.</summary>
		public int CompanyCodeDays { get; set; } = 7;

		/// <summary>Wrong codes one user may enter within <see cref="FailureWindowMinutes"/> before they are locked out for <see cref="LockoutMinutes"/>.</summary>
		public int MaxFailedAttempts { get; set; } = 5;
		public int FailureWindowMinutes { get; set; } = 15;
		public int LockoutMinutes { get; set; } = 15;
	}

	/// <summary>
	/// Makes and checks join codes. A code is 8 symbols from an alphabet without look-alikes, written XXXX-XXXX. The database keeps only
	/// <see cref="Hash"/> of it (HMAC-SHA256 with a key derived from the server secret, over the code without dashes and in upper case),
	/// so a copy of the table does not give the codes. The plain code exists in the email and in the answer to the request that made it.
	/// </summary>
	public sealed class JoinCodes
	{
		private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

		private readonly byte[] mKey;

		public JoinCodes(IConfiguration configuration)
		{
			var secret = configuration["Join:CodeKey"];
			if (string.IsNullOrWhiteSpace(secret)) secret = configuration["Jwt:SigningKey"];
			if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("Join codes need 'Join:CodeKey' or 'Jwt:SigningKey' to be configured.");
			// Not the signing key itself: a leak of one use must not be a leak of the other.
			mKey = SHA256.HashData(Encoding.UTF8.GetBytes("join-code-key:" + secret));
		}

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

		public string Hash(string normalizedCode) => Convert.ToHexStringLower(HMACSHA256.HashData(mKey, Encoding.UTF8.GetBytes(normalizedCode)));
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
