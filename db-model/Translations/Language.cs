using System;

namespace db_model.Translations
{
    /// <summary>
    /// A target language available for translation. English is never a row here — it's
    /// the source language every TranslationToken.Text is written in, so it never needs
    /// a Translation row either.
    /// </summary>
    public class Language
    {
        public const string English = "en";

        public string Code { get; set; } = default!;
        public string EnglishName { get; set; } = default!;
        public string NativeName { get; set; } = default!;
        public bool IsEnabled { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
