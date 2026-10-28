using db_model.UserManagement;

namespace api_server.Auth
{
    /// <summary>
    /// Delivers a freshly generated password reset key to the user (e-mail, SMS...).
    /// The key exists in plain text only here and in the delivered message.
    /// </summary>
    public interface IPasswordResetKeySender
    {
        /// <param name="recipientEmail">
        /// Where to send it. Passed explicitly because a not-yet-activated user's stored e-mail
        /// carries a registration prefix and is not a deliverable address. Null or empty when the
        /// user has no e-mail address (a password-reset request for such a user still creates a
        /// key); the sender decides whether and how else to reach them.
        /// </param>
        Task SendAsync(User user, string? recipientEmail, string resetKey, DateTime expiresAtUtc, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Placeholder until an e-mail channel exists. With nothing to deliver it, the key goes to the
    /// log — which is the only way to get at it. That makes the log as sensitive as a password
    /// database (whoever can read it can set any user's password): replace this sender, and with
    /// it the logging, before real users depend on the reset flow.
    /// </summary>
    public class NoDeliveryPasswordResetKeySender(ILogger<NoDeliveryPasswordResetKeySender> logger) : IPasswordResetKeySender
    {
        public Task SendAsync(User user, string? recipientEmail, string resetKey, DateTime expiresAtUtc, CancellationToken cancellationToken)
        {
            logger.LogWarning(
                "Password reset key issued for user {UserId} (expires {ExpiresAt:u}, has e-mail: {HasEmail}) but no delivery channel is configured. " +
                // ":l" = literal: without it Serilog renders a string property in quotes, and the key in
                // the log would no longer be the exact key (tests read it from here).
                "Reset key: {ResetKey:l} — portal link path: {ResetLinkPath:l}",
                user.Id, expiresAtUtc, !string.IsNullOrWhiteSpace(recipientEmail), resetKey, $"/ResetPassword/{resetKey}");
            return Task.CompletedTask;
        }
    }
}
