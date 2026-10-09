using System.Globalization;

namespace FluentSakhrDictionary;

/// <summary>UI display language: Arabic and English. Default follows the Windows display
/// language (Arabic device -> Arabic UI), overridable in Settings. Apply on startup and on change.</summary>
public static class Loc
{
    public static bool IsArabic { get; private set; }

    public static void Apply(Settings s)
    {
        IsArabic = s.Language switch
        {
            "ar" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar",
        };
    }

    /// <summary>Pair-style lookup: T(english, arabic).</summary>
    public static string T(string en, string ar) => IsArabic ? ar : en;

    public static string Meanings(int n) => IsArabic ? (n == 1 ? "معنى واحد" : n + " معنى") : (n == 1 ? "1 meaning" : n + " meanings");
    public static string EnglishWords(int n) => IsArabic ? (n == 1 ? "كلمة إنجليزية واحدة" : n + " كلمات إنجليزية") : (n == 1 ? "1 English word" : n + " English words");
    public static string MeaningsFromWiktionary(int n) => IsArabic ? (n == 1 ? "معنى واحد من Wiktionary" : n + " معاني من Wiktionary") : (n == 1 ? "1 meaning from Wiktionary" : n + " meanings from Wiktionary");
    public static string NotFound => T("Not found.", "غير موجود.");
}
