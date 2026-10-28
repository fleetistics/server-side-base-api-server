namespace api_server.Auth
{
    /// <summary>
    /// New-password rules following NIST SP 800-63B: length and a deny-list rather than
    /// composition rules (no "must contain a digit/symbol"), every character allowed,
    /// nothing trimmed or truncated. Each failed rule yields a human-readable explanation.
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;

        // Upper bound only to keep a single request from forcing huge Argon2 inputs.
        public const int MaxLength = 128;

        // Most common leaked passwords that still meet the length rule.
        private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "password1", "password12", "password123", "passw0rd", "p@ssw0rd", "p@ssword",
            "12345678", "123456789", "1234567890", "0123456789", "11111111", "00000000", "87654321",
            "qwertyui", "qwerty123", "qwertyuiop", "qwerty12", "1q2w3e4r", "1qaz2wsx", "zaq12wsx",
            "abcd1234", "abc12345", "abcdefgh", "iloveyou", "letmein1", "welcome1", "welcome123",
            "admin123", "administrator", "changeme", "trustno1", "football", "baseball", "superman",
            "monkey123", "dragon123", "sunshine", "princess", "starwars", "whatever", "mustang1",
            "login123", "master123", "access14", "shadow12", "michael1", "jennifer", "computer",
            "internet", "samsung1", "fleetistics",
        };

        /// <summary>Returns the explanations of every rule the password breaks; empty when it is acceptable.</summary>
        /// <param name="contextTerms">User name / e-mail local part etc. the password must not simply echo.</param>
        public static IReadOnlyList<string> Validate(string? password, params string?[] contextTerms)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(password))
            {
                errors.Add("Password is required.");
                return errors;
            }

            if (password.Length < MinLength)
            {
                errors.Add($"Password must be at least {MinLength} characters long.");
            }
            if (password.Length > MaxLength)
            {
                errors.Add($"Password must be at most {MaxLength} characters long.");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                errors.Add("Password must not consist only of whitespace.");
            }
            else if (password.Length >= MinLength && password.Distinct().Count() == 1)
            {
                errors.Add("Password must not be a single repeated character.");
            }
            if (CommonPasswords.Contains(password))
            {
                errors.Add("Password is too common; choose something less predictable.");
            }

            foreach (var term in contextTerms)
            {
                // Short terms ("ab") would reject far too many unrelated passwords.
                if (term is { Length: >= 4 } && password.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Password must not contain your user name or e-mail address.");
                    break;
                }
            }

            return errors;
        }
    }
}
