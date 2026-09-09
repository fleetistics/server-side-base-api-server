using System;

namespace db_model.UserManagement
{
    /// <summary>Refresh-token bookkeeping for a session: keys, lifetime, and rotation.</summary>
    public class SessionTokenInfo
    {
        //public string SessionKey { get; set; } = string.Empty;
        public string SessionRotationKey { get; set; } = string.Empty;

        // The key SessionRotationKey replaced, kept only for the reuse-grace window
        // (see JwtOptions.RefreshReuseGraceSeconds): lets a duplicate/racing request that
        // still carries it be treated as a harmless retry instead of a stolen-cookie replay.
        public string PreviousSessionRotationKey { get; set; } = string.Empty;

        public DateTime CreationTime { get; set; }
        public DateTime ExpirationTime { get; set; }
        public DateTime LatestRefreshTime { get; set; }
    }
}
