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

/// <summary>One Arabic term: real Wiktionary glosses when they exist, plus the 1996 Sakhr
/// reverse-index English lemmas as a clearly-separated supplement. Never interleaved.</summary>
public record ArabicResult(string ArabicTerm, string[] Glosses, string Pos, string[] SakhrLemmas)
{
    public string DisplayWord => ArabicTerm;
    public bool FromWiktionary => Glosses.Length > 0;
}

/// <summary>Loads the embedded Sakhr dataset once and answers instant prefix/exact lookups over a sorted array, both directions.</summary>
public static class DictionaryService
{
    static Entry[] _entries = Array.Empty<Entry>();
    static string[] _words = Array.Empty<string>();
    static string[] _revKeys = Array.Empty<string>();
    static RevBucket[] _revBuckets = Array.Empty<RevBucket>();
    static string[] _wikKeys = Array.Empty<string>();
    static WikBucket[] _wikBuckets = Array.Empty<WikBucket>();
    static string[] _infForms = Array.Empty<string>();
    static InfBucket[] _infBuckets = Array.Empty<InfBucket>();
    static readonly object _gate = new();
    static bool _loaded;

    sealed class RevBucket
    {
        public string Display = "";
        public readonly List<string> Lemmas = new();
    }

    sealed class WikBucket
    {
        public string Display = "";
        public string Pos = "";
        public readonly List<string> Glosses = new();
    }

    sealed class InfBucket
    {
        public string Lemma = "";
        public string Note = "";
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

            // Wiktionary Arabic-English data (CC BY-SA 4.0). Optional file: the app still
            // works from the 1996 reverse index alone when it is absent.
            var wik = new Dictionary<string, WikBucket>(StringComparer.Ordinal);
            string wikPath = Path.Combine(AppContext.BaseDirectory, "data", "wiktionary-ar.jsonl");
            if (File.Exists(wikPath))
            {
                foreach (var line in File.ReadLines(wikPath))
                {
                    if (line.Length == 0) continue;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string word = root.GetProperty("word").GetString() ?? "";
                    string pos = root.TryGetProperty("pos", out var p) ? p.GetString() ?? "" : "";
                    string key = NormalizeArabic(word);
                    if (key.Length == 0) continue;
                    if (!wik.TryGetValue(key, out var bucket))
                    {
                        bucket = new WikBucket { Display = word, Pos = pos };
                        wik[key] = bucket;
                    }
                    else if (pos.Length > 0 && !bucket.Pos.Split(" \u00b7 ").Contains(pos))
                    {
                        bucket.Pos = bucket.Pos.Length == 0 ? pos : bucket.Pos + " \u00b7 " + pos;
                    }
                    foreach (var g in root.GetProperty("glosses").EnumerateArray())
                    {
                        string? gloss = g.GetString();
                        if (!string.IsNullOrWhiteSpace(gloss) && !bucket.Glosses.Contains(gloss)) bucket.Glosses.Add(gloss);
                    }
                }
            }
            _wikKeys = wik.Keys.ToArray();
            Array.Sort(_wikKeys, StringComparer.Ordinal);
            _wikBuckets = _wikKeys.Select(k => wik[k]).ToArray();

            // English inflections (Wiktionary form -> lemma, CC BY-SA 4.0), so a typed
            // form like ABANDONS resolves to ABANDON. Optional file; absent = no redirect.
            var inf = new Dictionary<string, InfBucket>(StringComparer.Ordinal);
            string infPath = Path.Combine(AppContext.BaseDirectory, "data", "inflections-en.jsonl");
            if (File.Exists(infPath))
            {
                foreach (var line in File.ReadLines(infPath))
                {
                    if (line.Length == 0) continue;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string form = root.GetProperty("form").GetString() ?? "";
                    string lemma = root.GetProperty("lemma").GetString() ?? "";
                    string note = root.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                    if (form.Length == 0 || lemma.Length == 0) continue;
                    if (!inf.ContainsKey(form)) inf[form] = new InfBucket { Lemma = lemma, Note = note };
                }
            }
            _infForms = inf.Keys.ToArray();
            Array.Sort(_infForms, StringComparer.Ordinal);
            _infBuckets = _infForms.Select(k => inf[k]).ToArray();
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

    /// <summary>Arabic -&gt; English: merges prefix windows over the Wiktionary index and the
    /// 1996 Sakhr reverse index. Wiktionary glosses win the entry; Sakhr lemmas ride along as
    /// a labeled supplement, so the two sources never interleave or duplicate.</summary>
    public static IReadOnlyList<ArabicResult> SearchArabic(string query, int max = 200)
    {
        EnsureLoaded();
        string q = NormalizeArabic(query);
        if (q.Length == 0) return Array.Empty<ArabicResult>();
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        CollectPrefix(_wikKeys, q, keys, max);
        CollectPrefix(_revKeys, q, keys, max);
        var list = new List<ArabicResult>();
        foreach (string key in keys)
        {
            if (list.Count >= max) break;
            string display = "", pos = "";
            string[] glosses = Array.Empty<string>(), lemmas = Array.Empty<string>();
            int wi = Array.BinarySearch(_wikKeys, key, StringComparer.Ordinal);
            if (wi >= 0)
            {
                var b = _wikBuckets[wi];
                display = b.Display;
                pos = b.Pos;
                glosses = b.Glosses.ToArray();
            }
            int ri = Array.BinarySearch(_revKeys, key, StringComparer.Ordinal);
            if (ri >= 0)
            {
                lemmas = _revBuckets[ri].Lemmas.ToArray();
                if (display.Length == 0) display = _revBuckets[ri].Display;
            }
            list.Add(new ArabicResult(display, glosses, pos, lemmas));
        }
        return list;
    }

    static void CollectPrefix(string[] keys, string q, SortedSet<string> into, int max)
    {
        int lo = 0, hi = keys.Length - 1, first = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int cmp = string.CompareOrdinal(keys[mid], q);
            bool starts = cmp >= 0 && keys[mid].StartsWith(q, StringComparison.Ordinal);
            if (starts) { first = mid; hi = mid - 1; }
            else if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }
        if (first < 0) return;
        for (int i = first; i < keys.Length && keys[i].StartsWith(q, StringComparison.Ordinal) && into.Count < max; i++)
            into.Add(keys[i]);
    }

    public static Entry? Exact(string word)
    {
        EnsureLoaded();
        word = word.Trim().ToUpperInvariant();
        int i = Array.BinarySearch(_words, word, StringComparer.Ordinal);
        return i >= 0 ? _entries[i] : null;
    }

    /// <summary>English inflection -> base lemma (Wiktionary, CC BY-SA 4.0). Null when the
    /// query is not a known form of a word we carry. Lemma is the raw uppercase word.</summary>
    public static Inflection? InflectionOf(string query)
    {
        EnsureLoaded();
        query = query.Trim().ToUpperInvariant();
        if (query.Length == 0) return null;
        int i = Array.BinarySearch(_infForms, query, StringComparer.Ordinal);
        if (i < 0) return null;
        var b = _infBuckets[i];
        return new Inflection(Entry.TitleCase(query), b.Lemma, b.Note);
    }
}

public record Inflection(string FormDisplay, string Lemma, string Note);
