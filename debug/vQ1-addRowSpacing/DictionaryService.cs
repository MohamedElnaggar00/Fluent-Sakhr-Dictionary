using System.Text.Json;

namespace FluentSakhrDictionary;

public record Entry(string Word, string[] Meanings)
{
    public bool Found => Meanings.Length > 0;
}

/// <summary>Loads the embedded Sakhr dataset once and answers instant prefix/exact lookups over a sorted array.</summary>
public static class DictionaryService
{
    static Entry[] _entries = Array.Empty<Entry>();
    static string[] _words = Array.Empty<string>();
    static readonly object _gate = new();
    static bool _loaded;

    public static int Count => _entries.Length;

    public static void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_loaded) return;
            string path = Path.Combine(AppContext.BaseDirectory, "data", "dictionary.jsonl");
            var entries = new List<Entry>(64 * 1024);
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
                entries.Add(new Entry(word, meanings.ToArray()));
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
            _entries = entries.ToArray();
            _words = _entries.Select(e => e.Word).ToArray();
            _loaded = true;
        }
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

    public static Entry? Exact(string word)
    {
        EnsureLoaded();
        word = word.Trim().ToUpperInvariant();
        int i = Array.BinarySearch(_words, word, StringComparer.Ordinal);
        return i >= 0 ? _entries[i] : null;
    }
}
