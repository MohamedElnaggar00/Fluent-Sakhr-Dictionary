using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FluentSakhrDictionary;

public partial class SearchPage : Page
{
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    IReadOnlyList<Entry> _current = Array.Empty<Entry>();
    IReadOnlyList<ArabicResult> _currentRev = Array.Empty<ArabicResult>();
    Inflection? _currentInflection;
    ArabicInflection? _currentArInflection;
    bool _useWiktionary = true;

    public SearchPage()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSearch(); };
        Loaded += (_, _) => { DictionaryService.EnsureLoaded(); SearchBox.Focus(FocusState.Programmatic); };
    }

    public void FocusSearchBox() => SearchBox.Focus(FocusState.Programmatic);

    /// <summary>CI screenshot helper: type a word (English or Arabic) and show its translations.</summary>
    public void TypeAndSelect(string word)
    {
        SearchBox.Text = word;
        RunSearch();
        if (_current.Count > 0) { ResultsList.SelectedIndex = 0; ShowEntry(_current[0]); }
        else if (_currentRev.Count > 0) { ResultsList.SelectedIndex = 0; ShowReverse(_currentRev[0]); }
        else if (_currentInflection != null) ShowInflection(_currentInflection);
        else if (_currentArInflection != null) ShowReverseInflection(_currentArInflection);
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (_current.Count > 0) { ResultsList.SelectedIndex = 0; ShowEntry(_current[0]); }
            else if (_currentRev.Count > 0) { ResultsList.SelectedIndex = 0; ShowReverse(_currentRev[0]); }
            else if (_currentInflection != null) ShowInflection(_currentInflection);
        else if (_currentArInflection != null) ShowReverseInflection(_currentArInflection);
        }
        if (e.Key == Windows.System.VirtualKey.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
            ResultsList.Focus(FocusState.Programmatic);
        }
    }

    static bool HasArabic(string q)
    {
        foreach (char c in q) if (c >= '؀' && c <= 'ۿ') return true;
        return false;
    }

    void RunSearch()
    {
        string q = SearchBox.Text.Trim();
        _useWiktionary = Settings.Load().UseWiktionary;
        HomeOfflineText.Visibility = Visibility.Collapsed;
        if (q.Length == 0)
        {
            _current = Array.Empty<Entry>();
            _currentRev = Array.Empty<ArabicResult>();
            _currentInflection = null;
            _currentArInflection = null;
            ResultsList.ItemsSource = null;
            ResetPane();
            return;
        }
        if (HasArabic(q))
        {
            _current = Array.Empty<Entry>();
            _currentInflection = null;
            _currentArInflection = null;
            _currentRev = DictionaryService.SearchArabic(q);
            ResultsList.FlowDirection = FlowDirection.RightToLeft;
            ResultsList.ItemsSource = _currentRev;
            var exact = _currentRev.FirstOrDefault(x => DictionaryService.NormalizeArabic(x.ArabicTerm) == DictionaryService.NormalizeArabic(q));
            if (_currentRev.Count == 1 || exact != null)
            {
                var pick = exact ?? _currentRev[0];
                ResultsList.SelectedItem = pick;
                ShowReverse(pick);
            }
            else if (_useWiktionary && DictionaryService.InflectionOfArabic(q) is { } arinf)
            {
                // Conjugated/plural Arabic form not in either index -> show its lemma (يكتب -> كتب).
                _currentArInflection = arinf;
                ShowReverseInflection(arinf);
            }
            else if (_currentRev.Count == 0) ShowNotFound(q);
        }
        else
        {
            _currentRev = Array.Empty<ArabicResult>();
            _current = DictionaryService.Search(q);
            ResultsList.FlowDirection = FlowDirection.LeftToRight;
            ResultsList.ItemsSource = _current;
            var exact = _current.FirstOrDefault(x => x.Word.Equals(q, StringComparison.OrdinalIgnoreCase));
            _currentArInflection = null;
            if (exact is { Found: true })
            {
                ResultsList.SelectedItem = exact;
                ShowEntry(exact);
                _currentInflection = null;
            }
            else if (_useWiktionary && DictionaryService.InflectionOf(q) is { } inf && DictionaryService.Exact(inf.Lemma) is { Found: true })
            {
                // Unknown word or empty 1996 miss-record, but a known inflection (ABANDONS -> ABANDON).
                _currentInflection = inf;
                ShowInflection(inf);
            }
            else if (_current.Count == 1 || exact != null)
            {
                var pick = exact ?? _current[0];
                ResultsList.SelectedItem = pick;
                ShowEntry(pick);
                _currentInflection = null;
            }
            else
            {
                _currentInflection = null;
                if (_current.Count == 0) ShowNotFound(q);
            }
        }
    }

    /// <summary>Nothing matched: plain not-found pane.</summary>
    void ShowNotFound(string q)
    {
        WordTitle.FlowDirection = HasArabic(q) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        WordTitle.Text = q;
        WordSubtitle.Text = "";
        MeaningsRepeater.ItemsSource = null;
        MeaningsRepeater.Visibility = Visibility.Collapsed;
        EnglishRepeater.ItemsSource = null;
        EnglishRepeater.Visibility = Visibility.Collapsed;
        WikHeader.Visibility = Visibility.Collapsed;
        SakhrHeader.Visibility = Visibility.Collapsed;
        SakhrRepeater.ItemsSource = null;
        SakhrRepeater.Visibility = Visibility.Collapsed;
        NotFoundText.Text = "Not found.";
        NotFoundText.Visibility = Visibility.Visible;
    }

    void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (ResultsList.SelectedItem)
        {
            case Entry entry: ShowEntry(entry); break;
            case ArabicResult rev: ShowReverse(rev); break;
        }
    }

    void ShowEntry(Entry entry)
    {
        WordTitle.FlowDirection = FlowDirection.LeftToRight;
        WordTitle.Text = entry.DisplayWord;
        EnglishRepeater.ItemsSource = null;
        EnglishRepeater.Visibility = Visibility.Collapsed;
        WikHeader.Visibility = Visibility.Collapsed;
        SakhrHeader.Visibility = Visibility.Collapsed;
        SakhrRepeater.ItemsSource = null;
        SakhrRepeater.Visibility = Visibility.Collapsed;
        if (entry.Found)
        {
            WordSubtitle.Text = entry.Meanings.Length == 1 ? "1 meaning" : entry.Meanings.Length + " meanings";
            MeaningsRepeater.Visibility = Visibility.Visible;
            MeaningsRepeater.ItemsSource = entry.Meanings;
            NotFoundText.Visibility = Visibility.Collapsed;
        }
        else
        {
            // 1996 empty record: with Wiktionary on, show its filled translations.
            var tr = _useWiktionary ? DictionaryService.TranslationsOf(entry.Word) : null;
            if (tr is { Length: > 0 })
            {
                WordSubtitle.Text = tr.Length == 1 ? "1 meaning from Wiktionary" : tr.Length + " meanings from Wiktionary";
                MeaningsRepeater.Visibility = Visibility.Visible;
                MeaningsRepeater.ItemsSource = tr;
                NotFoundText.Visibility = Visibility.Collapsed;
            }
            else
            {
                WordSubtitle.Text = "";
                MeaningsRepeater.ItemsSource = null;
                NotFoundText.Text = "Not found.";
                NotFoundText.Visibility = Visibility.Visible;
            }
        }
    }

    /// <summary>Typed inflection resolved to its base lemma: show the lemma's meanings under
    /// the typed form, with a "form of" note (ABANDONS - third-person singular of ABANDON).</summary>
    void ShowInflection(Inflection inf)
    {
        var lemma = DictionaryService.Exact(inf.Lemma);
        if (lemma is not { Found: true }) { _currentInflection = null; ResetPane(); return; }
        WordTitle.FlowDirection = FlowDirection.LeftToRight;
        WordTitle.Text = inf.FormDisplay;
        EnglishRepeater.ItemsSource = null;
        EnglishRepeater.Visibility = Visibility.Collapsed;
        WikHeader.Visibility = Visibility.Collapsed;
        SakhrHeader.Visibility = Visibility.Collapsed;
        SakhrRepeater.ItemsSource = null;
        SakhrRepeater.Visibility = Visibility.Collapsed;
        string note = inf.Note.Length > 0 ? char.ToUpperInvariant(inf.Note[0]) + inf.Note[1..] : "Form";
        WordSubtitle.Text = note + " of " + Entry.TitleCase(inf.Lemma) + (lemma.Meanings.Length == 1 ? " - 1 meaning" : " - " + lemma.Meanings.Length + " meanings");
        MeaningsRepeater.Visibility = Visibility.Visible;
        MeaningsRepeater.ItemsSource = lemma.Meanings;
        NotFoundText.Visibility = Visibility.Collapsed;
    }

    void ShowReverse(ArabicResult rev)
    {
        WordTitle.FlowDirection = FlowDirection.RightToLeft;
        WordTitle.Text = rev.ArabicTerm;
        RenderArabicSections(rev);
    }

    /// <summary>Typed Arabic form resolved to its lemma (Wiktionary): the lemma's translations
    /// under the typed form, with a "form of" note in the subtitle (يكتب -> كتب).</summary>
    void ShowReverseInflection(ArabicInflection inf)
    {
        WordTitle.FlowDirection = FlowDirection.RightToLeft;
        WordTitle.Text = inf.FormDisplay;
        RenderArabicSections(inf.Lemma);
        string note = inf.Note.Length > 0 ? char.ToUpperInvariant(inf.Note[0]) + inf.Note[1..] : "Form";
        WordSubtitle.Text = note + " of " + inf.Lemma.ArabicTerm + (WordSubtitle.Text.Length > 0 ? " - " + WordSubtitle.Text : "");
    }

    void RenderArabicSections(ArabicResult rev)
    {
        MeaningsRepeater.ItemsSource = null;
        MeaningsRepeater.Visibility = Visibility.Collapsed;
        NotFoundText.Visibility = Visibility.Collapsed;
        var subtitle = new List<string>();
        if (rev.SakhrLemmas.Length > 0)
        {
            SakhrHeader.Visibility = Visibility.Visible;
            SakhrRepeater.Visibility = Visibility.Visible;
            SakhrRepeater.ItemsSource = rev.SakhrLemmas.Select(Entry.TitleCase).ToArray();
            subtitle.Add(rev.SakhrLemmas.Length == 1 ? "1 English word" : rev.SakhrLemmas.Length + " English words");
        }
        else
        {
            SakhrHeader.Visibility = Visibility.Collapsed;
            SakhrRepeater.Visibility = Visibility.Collapsed;
            SakhrRepeater.ItemsSource = null;
        }
        if (_useWiktionary && rev.FromWiktionary)
        {
            WikHeader.Visibility = Visibility.Visible;
            EnglishRepeater.Visibility = Visibility.Visible;
            EnglishRepeater.ItemsSource = rev.Glosses;
            int n = rev.Glosses.Length;
            subtitle.Add(n == 1 ? "1 meaning from Wiktionary" : n + " meanings from Wiktionary");
        }
        else
        {
            WikHeader.Visibility = Visibility.Collapsed;
            EnglishRepeater.Visibility = Visibility.Collapsed;
            EnglishRepeater.ItemsSource = null;
        }
        WordSubtitle.Text = (rev.Pos.Length > 0 ? rev.Pos + " - " : "") + string.Join(" + ", subtitle);
    }

    void ResetPane()
    {
        WordTitle.FlowDirection = FlowDirection.RightToLeft;
        WordTitle.Text = "قاموس صخر الحديث";
        WordSubtitle.Text = "Sakhr Dictionary Revive - English ⇄ Arabic";
        HomeOfflineText.Visibility = Visibility.Visible;
        MeaningsRepeater.Visibility = Visibility.Visible;
        MeaningsRepeater.ItemsSource = null;
        EnglishRepeater.ItemsSource = null;
        EnglishRepeater.Visibility = Visibility.Collapsed;
        WikHeader.Visibility = Visibility.Collapsed;
        SakhrHeader.Visibility = Visibility.Collapsed;
        SakhrRepeater.ItemsSource = null;
        SakhrRepeater.Visibility = Visibility.Collapsed;
        NotFoundText.Visibility = Visibility.Collapsed;
    }
}
