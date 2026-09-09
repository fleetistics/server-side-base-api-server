using System;

namespace db_model.Translations
{
    /// <summary>
    /// A known English UI string, keyed by its own text (gettext-style — the English
    /// wording itself is the lookup key, not a separate identifier). Discovered
    /// client-side at runtime and reported to the server; ReportCount/LastSeenAt track
    /// how often and how recently the client has encountered it since.
    /// </summary>
    public class TranslationToken
    {
        public int Id { get; set; }
        public string Text { get; set; } = default!;
        public string? Context { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public int ReportCount { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
