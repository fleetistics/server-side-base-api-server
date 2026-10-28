using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace api_server.Auth
{
    /// <summary>
    /// Per-account brake on password guessing, complementing the per-IP rate limit (which a
    /// distributed attack walks around). Keyed by the user name that was typed — not by the
    /// matching user — so an unknown name locks exactly like a real one and the lock reveals
    /// nothing about which accounts exist.
    ///
    /// State is in memory: it is per server instance and resets on restart. The trade-off of
    /// any account lock applies too — someone who knows a user name can keep that account
    /// locked — which the short lockout and the reset-password unlock are meant to soften.
    /// </summary>
    public class LoginThrottle(IOptions<AuthOptions> options, TimeProvider time)
    {
        // Distinct keys are attacker-controlled; past this many (after dropping expired ones)
        // new names are simply not tracked rather than growing without bound.
        private const int MaxTrackedAccounts = 50_000;

        private sealed class Entry
        {
            public int Failures;
            public DateTime WindowStartUtc;
            public DateTime LockedUntilUtc;
        }

        private readonly ConcurrentDictionary<string, Entry> _entries = new();

        private AuthOptions Options => options.Value;
        private DateTime NowUtc => time.GetUtcNow().UtcDateTime;

        public int MaxFailedAttempts => Options.LoginMaxFailedAttempts;

        /// <summary>How much longer the account stays locked; null when it is not locked.</summary>
        public TimeSpan? GetRetryAfter(string key)
        {
            if (Options.LoginMaxFailedAttempts <= 0 || !_entries.TryGetValue(key, out var entry))
            {
                return null;
            }
            lock (entry)
            {
                var remaining = entry.LockedUntilUtc - NowUtc;
                return remaining > TimeSpan.Zero ? remaining : null;
            }
        }

        /// <summary>Counts a failed attempt; LockedNow is true when this one tipped the account into a lock.</summary>
        public (int Failures, bool LockedNow) RegisterFailure(string key)
        {
            var max = Options.LoginMaxFailedAttempts;
            if (max <= 0)
            {
                return (0, false);
            }

            var now = NowUtc;
            if (_entries.Count >= MaxTrackedAccounts)
            {
                Purge(now);
                if (_entries.Count >= MaxTrackedAccounts && !_entries.ContainsKey(key))
                {
                    return (0, false);
                }
            }

            var entry = _entries.GetOrAdd(key, _ => new Entry());
            lock (entry)
            {
                if (entry.LockedUntilUtc > now)
                {
                    return (entry.Failures, false); // already locked; callers normally never get here
                }
                if (entry.Failures == 0 || entry.LockedUntilUtc != default || now - entry.WindowStartUtc > Options.LoginFailureWindow)
                {
                    // First failure, a lock that has run out, or an old window: start counting afresh.
                    entry.Failures = 0;
                    entry.WindowStartUtc = now;
                    entry.LockedUntilUtc = default;
                }

                entry.Failures++;
                if (entry.Failures >= max)
                {
                    entry.LockedUntilUtc = now + Options.LoginLockout;
                    return (entry.Failures, true);
                }
                return (entry.Failures, false);
            }
        }

        /// <summary>Forgets the account's failures and lock (after a successful login or password reset).</summary>
        public void Reset(string key) => _entries.TryRemove(key, out _);

        private void Purge(DateTime now)
        {
            foreach (var (key, entry) in _entries)
            {
                lock (entry)
                {
                    if (entry.LockedUntilUtc <= now && now - entry.WindowStartUtc > Options.LoginFailureWindow)
                    {
                        _entries.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}
