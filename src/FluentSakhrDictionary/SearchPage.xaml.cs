using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FluentSakhrDictionary;

public partial class SearchPage : Page
{
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    IReadOnlyList<Entry> _current = Array.Empty<Entry>();
    IReadOnlyList<ReverseResult> _currentRev = Array.Empty<ReverseResult>();

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
        if (q.Length == 0)
        {
            _current = Array.Empty<Entry>();
            _currentRev = Array.Empty<ReverseResult>();
            ResultsList.ItemsSource = null;
            ResetPane();
            return;
        }
        if (HasArabic(q))
        {
            _current = Array.Empty<Entry>();
            _currentRev = DictionaryService.ReverseSearch(q);
            ResultsList.FlowDirection = FlowDirection.RightToLeft;
            ResultsList.ItemsSource = _currentRev;
            var exact = _currentRev.FirstOrDefault(x => DictionaryService.NormalizeArabic(x.ArabicTerm) == DictionaryService.NormalizeArabic(q));
            if (_currentRev.Count == 1 || exact != null)
            {
                var pick = exact ?? _currentRev[0];
                ResultsList.SelectedItem = pick;
                ShowReverse(pick);
            }
        }
        else
        {
            _currentRev = Array.Empty<ReverseResult>();
            _current = DictionaryService.Search(q);
            ResultsList.FlowDirection = FlowDirection.LeftToRight;
            ResultsList.ItemsSource = _current;
            if (_current.Count == 1 || _current.Any(x => x.Word.Equals(q, StringComparison.OrdinalIgnoreCase)))
            {
                var exact = _current.FirstOrDefault(x => x.Word.Equals(q, StringComparison.OrdinalIgnoreCase)) ?? _current[0];
                ResultsList.SelectedItem = exact;
                ShowEntry(exact);
            }
        }
    }

    void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (ResultsList.SelectedItem)
        {
            case Entry entry: ShowEntry(entry); break;
            case ReverseResult rev: ShowReverse(rev); break;
        }
    }

    void ShowEntry(Entry entry)
    {
        WordTitle.FlowDirection = FlowDirection.LeftToRight;
        WordTitle.Text = entry.DisplayWord;
        EnglishRepeater.ItemsSource = null;
        EnglishRepeater.Visibility = Visibility.Collapsed;
        if (entry.Found)
        {
            WordSubtitle.Text = entry.Meanings.Length == 1 ? "1 meaning" : entry.Meanings.Length + " meanings";
            MeaningsRepeater.Visibility = Visibility.Visible;
            MeaningsRepeater.ItemsSource = entry.Meanings;
            NotFoundText.Visibility = Visibility.Collapsed;
        }
        else
        {
            WordSubtitle.Text = "";
            MeaningsRepeater.ItemsSource = null;
            NotFoundText.Text = entry.DisplayWord + " is not in the original 1996 Sakhr search index (usually a rare inflection or a proper noun).";
            NotFoundText.Visibility = Visibility.Visible;
        }
    }

    void ShowReverse(ReverseResult rev)
    {
        WordTitle.FlowDirection = FlowDirection.RightToLeft;
        WordTitle.Text = rev.ArabicTerm;
        WordSubtitle.Text = rev.EnglishLemmas.Length == 1 ? "1 English word" : rev.EnglishLemmas.Length + " English words";
        MeaningsRepeater.ItemsSource = null;
        MeaningsRepeater.Visibility = Visibility.Collapsed;
        NotFoundText.Visibility = Visibility.Collapsed;
        EnglishRepeater.Visibility = Visibility.Visible;
        EnglishRepeater.ItemsSource = rev.EnglishLemmas.Select(Entry.TitleCase).ToArray();
    }

    void ResetPane()
    {
        WordTitle.FlowDirection = FlowDirection.RightToLeft;
        WordTitle.Text = "قاموس صخر";
        WordSubtitle.Text = "Fluent Sakhr Dictionary - 59,427 lemmas from the original 1996 Sakhr dictionary, English and Arabic";
        MeaningsRepeater.Visibility = Visibility.Visible;
        MeaningsRepeater.ItemsSource = null;
        EnglishRepeater.ItemsSource = null;
        EnglishRepeater.Visibility = Visibility.Collapsed;
        NotFoundText.Visibility = Visibility.Collapsed;
    }
}
