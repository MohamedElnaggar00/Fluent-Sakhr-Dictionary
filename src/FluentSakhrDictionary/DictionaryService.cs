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
    static string[] _trForms = Array.Empty<string>();
    static string[][] _trBuckets = Array.Empty<string[]>();
    static InfBucket[] _infBuckets = Array.Empty<InfBucket>();
    static readonly Dictionary<string, (string Lemma, string Note)> _infAr = new(StringComparer.Ordinal);
    static readonly object _gate = new();
    static Task? _coreTask;
    static Task? _wikTask;

    /// <summary>Core 1996 data (plus tiny translations table). Kicked off at app start on a
    /// background thread so the window paints instantly; searches await it.</summary>
    public static Task CoreReady
    {
        get { lock (_gate) return _coreTask ??= Task.Run(LoadCore); }
    }

    /// <summary>Wiktionary extras (Arabic index + both inflection tables, ~35MB). Loaded on
    /// first use and only when the Wiktionary toggle is on; Sakhr-only mode never pays for it.</summary>
    public static Task WikReady
    {
        get { lock (_gate) return _wikTask ??= Task.Run(LoadWik); }
    }

    static void EnsureCore() => CoreReady.GetAwaiter().GetResult();
    static void EnsureWik() => WikReady.GetAwaiter().GetResult();

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

    static void LoadCore()
    {
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

            // Arabic translations filling the 1996 index's empty records (Wiktionary
            // translation tables + exact gloss matches, CC BY-SA 4.0). Optional file.
            var tr = new Dictionary<string, string[]>(StringComparer.Ordinal);
            string trPath = Path.Combine(AppContext.BaseDirectory, "data", "translations-en-ar.jsonl");
            if (File.Exists(trPath))
            {
                foreach (var line in File.ReadLines(trPath))
                {
                    if (line.Length == 0) continue;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string w = root.GetProperty("word").GetString() ?? "";
                    var ms = root.GetProperty("meanings").EnumerateArray().Select(m => m.GetString() ?? "").Where(m => m.Length > 0).ToArray();
                    if (w.Length > 0 && ms.Length > 0) tr[w] = ms;
                }
            }
            _trForms = tr.Keys.ToArray();
            Array.Sort(_trForms, StringComparer.Ordinal);
            _trBuckets = _trForms.Select(k => tr[k]).ToArray();
    }

    static void LoadWik()
    {
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

            // Arabic inflections (same Wiktionary source): normalized form -> (lemma, note).
            string infArPath = Path.Combine(AppContext.BaseDirectory, "data", "inflections-ar.jsonl");
            if (File.Exists(infArPath))
            {
                foreach (var line in File.ReadLines(infArPath))
                {
                    if (line.Length == 0) continue;
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    string form = root.GetProperty("form").GetString() ?? "";
                    string lemma = root.GetProperty("lemma").GetString() ?? "";
                    string note = root.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                    string key = NormalizeArabic(form);
                    if (key.Length == 0 || lemma.Length == 0) continue;
                    if (!_infAr.ContainsKey(key)) _infAr[key] = (lemma, note);
                }
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
        EnsureCore();
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
    public static IReadOnlyList<ArabicResult> SearchArabic(string query, bool includeWik, int max = 200)
    {
        EnsureCore();
        if (includeWik) EnsureWik();
        string q = NormalizeArabic(query);
        if (q.Length == 0) return Array.Empty<ArabicResult>();
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        if (includeWik) CollectPrefix(_wikKeys, q, keys, max);
        CollectPrefix(_revKeys, q, keys, max);
        var list = new List<ArabicResult>();
        foreach (string key in keys)
        {
            if (list.Count >= max) break;
            if (ArabicByKey(key) is { } result) list.Add(result);
        }
        return list;
    }

    /// <summary>Assembles one Arabic result from the Wiktionary and Sakhr reverse indexes for
    /// an already-normalized key; null when neither index carries it.</summary>
    static ArabicResult? ArabicByKey(string key)
    {
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
        if (wi < 0 && ri < 0) return null;
        return new ArabicResult(display, glosses, pos, lemmas);
    }

    /// <summary>Arabic inflection -> base lemma (Wiktionary, CC BY-SA 4.0), e.g. a conjugated
    /// verb or plural back to its dictionary form. Null when unknown or the lemma is absent.</summary>
    public static ArabicInflection? InflectionOfArabic(string query)
    {
        EnsureWik();
        EnsureCore();
        string q = NormalizeArabic(query);
        if (q.Length == 0 || !_infAr.TryGetValue(q, out var hit)) return null;
        if (ArabicByKey(NormalizeArabic(hit.Lemma)) is not { } lemma) return null;
        return new ArabicInflection(query.Trim(), lemma, hit.Note);
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
        EnsureCore();
        word = word.Trim().ToUpperInvariant();
        int i = Array.BinarySearch(_words, word, StringComparer.Ordinal);
        return i >= 0 ? _entries[i] : null;
    }

    /// <summary>English inflection -> base lemma (Wiktionary, CC BY-SA 4.0). Null when the
    /// query is not a known form of a word we carry. Lemma is the raw uppercase word.</summary>
    /// <summary>Arabic translations for a 1996 empty record, or null. Callers gate on
    /// the Wiktionary toggle: Sakhr-only mode never sees these.</summary>
    public static string[]? TranslationsOf(string word)
    {
        EnsureCore();
        word = word.Trim().ToUpperInvariant();
        if (word.Length == 0) return null;
        int i = Array.BinarySearch(_trForms, word, StringComparer.Ordinal);
        return i < 0 ? null : _trBuckets[i];
    }

    public static Inflection? InflectionOf(string query)
    {
        EnsureWik();
        query = query.Trim().ToUpperInvariant();
        if (query.Length == 0) return null;
        int i = Array.BinarySearch(_infForms, query, StringComparer.Ordinal);
        if (i < 0) return null;
        var b = _infBuckets[i];
        return new Inflection(Entry.TitleCase(query), b.Lemma, b.Note);
    }
}

public record Inflection(string FormDisplay, string Lemma, string Note);

public record ArabicInflection(string FormDisplay, ArabicResult Lemma, string Note);
