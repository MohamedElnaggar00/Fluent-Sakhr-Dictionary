using System.Text;
using System.Text.Json;

namespace FluentSakhrDictionary;

public record Entry(string Word, string[] Meanings)
{
    public bool Found => Meanings.Length > 0;
    public string DisplayWord => TitleCase(Word);

    /// <summary>"CAT FOOD" -&gt; "Cat Food": first letter of each word capital, the rest small.</summary>
    public static string TitleCase(string w)
    {
        var parts = w.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Length == 1
                ? parts[i].ToUpperInvariant()
                : char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
        return string.Join(' ', parts);
    }
}

/// <summary>One Arabic term and every English lemma whose 1996 Sakhr meanings contain it.</summary>
public record ReverseResult(string ArabicTerm, string[] EnglishLemmas)
{
    public string DisplayWord => ArabicTerm;
}

/// <summary>Loads the embedded Sakhr dataset once and answers instant prefix/exact lookups over a sorted array, both directions.</summary>
public static class DictionaryService
{
    static Entry[] _entries = Array.Empty<Entry>();
    static string[] _words = Array.Empty<string>();
    static string[] _revKeys = Array.Empty<string>();
    static RevBucket[] _revBuckets = Array.Empty<RevBucket>();
    static readonly object _gate = new();
    static bool _loaded;

    sealed class RevBucket
    {
        public string Display = "";
        public readonly List<string> Lemmas = new();
    }

    public static int Count => _entries.Length;

    public static void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_loaded) return;
            string path = Path.Combine(AppContext.BaseDirectory, "data", "dictionary.jsonl");
            var entries = new List<Entry>(64 * 1024);
            var rev = new Dictionary<string, RevBucket>(StringComparer.Ordinal);

            void IndexArabic(string raw, string lemma)
            {
                string key = NormalizeArabic(raw);
                if (key.Length == 0) return;
                if (!rev.TryGetValue(key, out var bucket))
                {
                    bucket = new RevBucket { Display = raw.Trim() };
                    rev[key] = bucket;
                }
                if (!bucket.Lemmas.Contains(lemma)) bucket.Lemmas.Add(lemma);
            }

            foreach (var line in File.ReadLines(path))
            {
                if (line.Length == 0) continue;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                string word = root.GetProperty("word").GetString() ?? "";
                var meanings = new List<string>();
                foreach (var m in root.GetProperty("meanings").EnumerateArray())
                {
                    string? s = m.GetString();
                    // Skip legacy junk rows from the 1996 data that carry no Arabic at all.
                    if (!string.IsNullOrWhiteSpace(s) && s.Any(c => c >= '؀' && c <= 'ۿ')) meanings.Add(s);
                }
                var entry = new Entry(word, meanings.ToArray());
                entries.Add(entry);
                // Reverse index: the full meaning plus its individual words, so both
                // "امرأة خبيثة" and a lone "خبيثة" lead back to CAT.
                foreach (var meaning in entry.Meanings)
                {
                    IndexArabic(meaning, entry.Word);
                    foreach (var token in meaning.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                        if (token.Length > 1) IndexArabic(token, entry.Word);
                }
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
            _entries = entries.ToArray();
            _words = _entries.Select(e => e.Word).ToArray();
            _revKeys = rev.Keys.ToArray();
            Array.Sort(_revKeys, StringComparer.Ordinal);
            _revBuckets = _revKeys.Select(k => rev[k]).ToArray();
            _loaded = true;
        }
    }

    /// <summary>Strips tashkeel/tatweel, unifies hamzated alefs and friends, keeps Arabic letters and single spaces only.</summary>
    public static string NormalizeArabic(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool pendingSpace = false;
        foreach (char c in s)
        {
            if (char.IsWhiteSpace(c)) { if (sb.Length > 0) pendingSpace = true; continue; }
            char mapped = c switch
            {
                'ً' or 'ٌ' or 'ٍ' or 'َ' or 'ُ' or 'ِ' or 'ّ' or 'ْ' or 'ٰ' or 'ـ' => '\0',
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' or 'ئ' => 'ي',
                'ؤ' => 'و',
                _ => c,
            };
            if (mapped == '\0') continue;
            if (mapped >= 'ء' && mapped <= 'ي')
            {
                if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
                sb.Append(mapped);
            }
        }
        return sb.ToString();
    }

    /// <summary>Binary-searched prefix window, then a bounded substring sweep if the prefix matched nothing.</summary>
    public static IReadOnlyList<Entry> Search(string query, int max = 200)
    {
        EnsureLoaded();
        query = query.Trim().ToUpperInvariant();
        if (query.Length == 0) return Array.Empty<Entry>();
        int lo = 0, hi = _words.Length - 1, first = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int cmp = string.CompareOrdinal(_words[mid], query);
            bool starts = cmp >= 0 && _words[mid].StartsWith(query, StringComparison.Ordinal);
            if (starts) { first = mid; hi = mid - 1; }
            else if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        if (first >= 0)
        {
            var list = new List<Entry>();
            for (int i = first; i < _words.Length && _words[i].StartsWith(query, StringComparison.Ordinal) && list.Count < max; i++)
                list.Add(_entries[i]);
            return list;
        }
        var fallback = new List<Entry>();
        for (int i = 0; i < _words.Length && fallback.Count < max; i++)
            if (_words[i].Contains(query, StringComparison.Ordinal)) fallback.Add(_entries[i]);
        return fallback;
    }

    /// <summary>Arabic -&gt; English: prefix window over the normalized reverse index built from the 1996 meanings.</summary>
    public static IReadOnlyList<ReverseResult> ReverseSearch(string query, int max = 200)
    {
        EnsureLoaded();
        string q = NormalizeArabic(query);
        if (q.Length == 0) return Array.Empty<ReverseResult>();
        int lo = 0, hi = _revKeys.Length - 1, first = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int cmp = string.CompareOrdinal(_revKeys[mid], q);
            bool starts = cmp >= 0 && _revKeys[mid].StartsWith(q, StringComparison.Ordinal);
            if (starts) { first = mid; hi = mid - 1; }
            else if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        var list = new List<ReverseResult>();
        if (first >= 0)
        {
            for (int i = first; i < _revKeys.Length && _revKeys[i].StartsWith(q, StringComparison.Ordinal) && list.Count < max; i++)
                list.Add(new ReverseResult(_revBuckets[i].Display, _revBuckets[i].Lemmas.ToArray()));
        }
        return list;
    }

    public static Entry? Exact(string word)
    {
        EnsureLoaded();
        word = word.Trim().ToUpperInvariant();
        int i = Array.BinarySearch(_words, word, StringComparer.Ordinal);
        return i >= 0 ? _entries[i] : null;
    }
}
