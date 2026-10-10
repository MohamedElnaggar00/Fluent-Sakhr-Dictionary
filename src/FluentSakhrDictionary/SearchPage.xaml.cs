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
        SearchBox.PlaceholderText = Loc.T("Type a word - اكتب كلمة", "اكتب كلمة - Type a word");
        SakhrHeader.Text = Loc.T("From the Sakhr dictionary:", "من قاموس صخر:");
        WikHeader.Text = Loc.T("From Wiktionary:", "من Wiktionary:");
        HomeOfflineText.Text = Loc.T("Works fully offline - no internet needed.", "يعمل تمامًا دون اتصال بالإنترنت.");
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RunSearch(); };
        Loaded += (_, _) => { _ = DictionaryService.CoreReady; SearchBox.Focus(FocusState.Programmatic); };
    }

    public void FocusSearchBox() => SearchBox.Focus(FocusState.Programmatic);

    /// <summary>CI screenshot helper: type a word (English or Arabic) and show its translations.</summary>
    public async Task TypeAndSelect(string word)
    {
        SearchBox.Text = word;
        await RunSearch();
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

    async Task<Inflection?> InflectionOfEn(string q)
    {
        await DictionaryService.WikReady;
        return DictionaryService.InflectionOf(q);
    }

    static bool HasArabic(string q) => DictionaryService.HasArabicText(q);

    int _searchVersion;

    async Task RunSearch()
    {
        int version = ++_searchVersion;
        string q = SearchBox.Text.Trim();
        _useWiktionary = Settings.Load().UseWiktionary;
        await DictionaryService.CoreReady;
        if (version != _searchVersion) return;
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
            if (_useWiktionary)
            {
                await DictionaryService.WikReady;
                if (version != _searchVersion) return;
            }
            _currentRev = DictionaryService.SearchArabic(q, _useWiktionary);
            ResultsList.FlowDirection = FlowDirection.RightToLeft;
            ResultsList.ItemsSource = _currentRev;
            var exact = _currentRev.FirstOrDefault(x => DictionaryService.NormalizeArabic(x.ArabicTerm) == DictionaryService.NormalizeArabic(q));
            if (_currentRev.Count == 1 || exact != null)
            {
                var pick = exact ?? _currentRev[0];
                ResultsList.SelectedItem = pick;
                ShowReverse(pick);
            }
            else if (_useWiktionary && DictionaryService.InflectionOfArabic(q) is { } arinf)  // WikReady already awaited above
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
            else if (_useWiktionary && await InflectionOfEn(q) is { } inf && DictionaryService.Exact(inf.Lemma) is { Found: true })
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
        NotFoundText.Text = Loc.NotFound;
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
            WordSubtitle.FlowDirection = Loc.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        WordSubtitle.Text = (entry.Note.Length > 0 ? Loc.EntryNote(entry.Note) + " - " : "") + Loc.Meanings(entry.Meanings.Length);
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
                WordSubtitle.Text = Loc.MeaningsFromWiktionary(tr.Length);
                MeaningsRepeater.Visibility = Visibility.Visible;
                MeaningsRepeater.ItemsSource = tr;
                NotFoundText.Visibility = Visibility.Collapsed;
            }
            else
            {
                WordSubtitle.Text = "";
                MeaningsRepeater.ItemsSource = null;
                NotFoundText.Text = Loc.NotFound;
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
        WordSubtitle.FlowDirection = Loc.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        WordSubtitle.Text = note + " of " + Entry.TitleCase(inf.Lemma) + " - " + Loc.Meanings(lemma.Meanings.Length);
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
            subtitle.Add(Loc.EnglishWords(rev.SakhrLemmas.Length));
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
            subtitle.Add(Loc.MeaningsFromWiktionary(n));
        }
        else
        {
            WikHeader.Visibility = Visibility.Collapsed;
            EnglishRepeater.Visibility = Visibility.Collapsed;
            EnglishRepeater.ItemsSource = null;
        }
        WordSubtitle.FlowDirection = Loc.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        WordSubtitle.Text = (rev.Pos.Length > 0 && subtitle.Count > 0 ? rev.Pos + " - " : "") + string.Join(" + ", subtitle);
        if (subtitle.Count == 0)
        {
            NotFoundText.Text = Loc.NotFound;
            NotFoundText.Visibility = Visibility.Visible;
        }
    }

    void ResetPane()
    {
        WordTitle.FlowDirection = FlowDirection.RightToLeft;
        WordTitle.Text = "قاموس صخر الحديث";
        WordSubtitle.FlowDirection = FlowDirection.LeftToRight;
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
