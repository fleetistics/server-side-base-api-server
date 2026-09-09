using System;
using db_model.UserManagement;

namespace db_model.Translations
{
    /// <summary>
    /// One token's text in one target language. Rows are created eagerly — for every
    /// enabled Language, a NULL-TranslatedText row exists for every TranslationToken —
    /// so "no row" never has to be distinguished from "not translated yet" by callers.
    /// </summary>
    public class Translation
    {
        public int Id { get; set; }
        public int TokenId { get; set; }
        public string LanguageCode { get; set; } = default!;
        public string? TranslatedText { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime LatestUpdate { get; set; }

        public TranslationToken? Token { get; set; }
        public Language? Language { get; set; }
    }
}
