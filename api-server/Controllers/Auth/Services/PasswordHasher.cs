using Isopoh.Cryptography.Argon2;

namespace api_server.Auth
{
    /// <summary>
    /// Argon2id password hashing (RFC 9106). Output is the standard PHC string
    /// ($argon2id$v=19$m=...,t=...,p=...$salt$hash) with a random per-hash salt, so the
    /// parameters travel with each hash and can be raised later without breaking old ones.
    /// </summary>
    public class PasswordHasher
    {
        // RFC 9106 "second recommended option": 64 MiB, 3 passes, 1 lane.
        private const int TimeCost = 3;
        private const int MemoryCostKiB = 64 * 1024;
        private const int Parallelism = 1;

        private const string Argon2idPrefix = "$argon2id$";

        // What a login check verifies against when there is nothing real to verify (unknown or
        // inactive user, no password set, unusable stored value, absurdly long input) — so that
        // every outcome costs one Argon2 run with the current parameters. Built once, when the
        // singleton is created (Program.cs resolves it at startup, so no request pays for it).
        private readonly string _dummyHash;
        private const string DummyPassword = "dummy-password-for-equal-time-checks";

        public PasswordHasher()
        {
            _dummyHash = Hash(DummyPassword);
        }

        public string Hash(string password) =>
            Argon2.Hash(password, TimeCost, MemoryCostKiB, Parallelism, Argon2Type.HybridAddressing);

        public bool Verify(string encodedHash, string password) =>
            Argon2.Verify(encodedHash, password);

        /// <summary>
        /// Checks a login attempt in the same time whatever the outcome: exactly one Argon2
        /// verification is always performed, against the stored hash when there is a usable one
        /// and against a dummy hash otherwise. The result cannot tell a caller (by timing)
        /// whether the user exists, is allowed in, has a password, or merely typed it wrong.
        /// </summary>
        /// <param name="encodedHash">The stored value; null / not an Argon2id string means "nothing to check".</param>
        public bool VerifyEqualTime(string? encodedHash, string password)
        {
            // Over-long input never matches a password that passed the policy; it is also not
            // fed to Argon2 as-is. The choice depends only on the input, not on the account.
            var withinLength = password.Length <= PasswordPolicy.MaxLength;
            var candidate = withinLength ? password : DummyPassword;

            // The library answers "false" for a malformed hash without doing any work (and does not
            // throw), so well-formedness is checked up front: only a hash it will really compute
            // against counts as usable.
            var usable = IsWellFormed(encodedHash);
            var matches = false;
            if (usable)
            {
                try
                {
                    matches = Argon2.Verify(encodedHash!, candidate);
                }
                catch (Exception)
                {
                    usable = false; // equalised below
                }
            }
            if (!usable)
            {
                Argon2.Verify(_dummyHash, candidate);
            }

            return usable && withinLength && matches;
        }

        /// <summary>
        /// "$argon2id$v=19$m=..,t=..,p=..$salt$hash" with numeric parameters and base64 (unpadded)
        /// salt and hash — the shape <see cref="Hash"/> produces.
        /// </summary>
        private static bool IsWellFormed(string? encodedHash)
        {
            if (encodedHash is null || !encodedHash.StartsWith(Argon2idPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var parts = encodedHash.Split('$'); // "", "argon2id", "v=19", "m=..,t=..,p=..", salt, hash
            if (parts.Length != 6 || parts[2] != "v=19")
            {
                return false;
            }

            var parameters = parts[3].Split(',');
            if (parameters.Length != 3 ||
                !HasNumber(parameters[0], "m=") || !HasNumber(parameters[1], "t=") || !HasNumber(parameters[2], "p="))
            {
                return false;
            }

            return IsUnpaddedBase64(parts[4]) && IsUnpaddedBase64(parts[5]);

            static bool HasNumber(string text, string prefix) =>
                text.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(text.AsSpan(prefix.Length), out var n) && n > 0;

            static bool IsUnpaddedBase64(string text)
            {
                if (text.Length == 0 || text.Length % 4 == 1)
                {
                    return false;
                }
                var padded = text.PadRight(text.Length + (4 - text.Length % 4) % 4, '=');
                return Convert.TryFromBase64String(padded, new byte[padded.Length], out _);
            }
        }
    }
}
