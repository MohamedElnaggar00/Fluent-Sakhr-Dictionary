using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FluentSakhrDictionary;

public partial class SearchPage : Page
{
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    IReadOnlyList<Entry> _current = Array.Empty<Entry>();

    public SearchPage()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSearch(); };
        Loaded += (_, _) => { DictionaryService.EnsureLoaded(); SearchBox.Focus(FocusState.Programmatic); };
    }

    public void FocusSearchBox() => SearchBox.Focus(FocusState.Programmatic);

    /// <summary>CI screenshot helper: type a word and show its meanings.</summary>
    public void TypeAndSelect(string word)
    {
        SearchBox.Text = word;
        RunSearch();
        if (_current.Count > 0) { ResultsList.SelectedIndex = 0; ShowEntry(_current[0]); }
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && _current.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            ShowEntry(_current[0]);
        }
        if (e.Key == Windows.System.VirtualKey.Down && _current.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, _current.Count - 1);
            ResultsList.Focus(FocusState.Programmatic);
        }
    }

    void RunSearch()
    {
        string q = SearchBox.Text.Trim();
        if (q.Length == 0)
        {
            _current = Array.Empty<Entry>();
            ResultsList.ItemsSource = null;
            ResetPane();
            return;
        }
        _current = DictionaryService.Search(q);
        ResultsList.ItemsSource = _current;
        if (_current.Count == 1 || _current.Any(x => x.Word.Equals(q, StringComparison.OrdinalIgnoreCase)))
        {
            var exact = _current.FirstOrDefault(x => x.Word.Equals(q, StringComparison.OrdinalIgnoreCase)) ?? _current[0];
            ResultsList.SelectedItem = exact;
            ShowEntry(exact);
        }
    }

    void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is Entry entry) ShowEntry(entry);
    }

    void ShowEntry(Entry entry)
    {
        WordTitle.Text = entry.Word;
        if (entry.Found)
        {
            WordSubtitle.Text = entry.Meanings.Length == 1 ? "1 meaning" : entry.Meanings.Length + " meanings";
            MeaningsRepeater.ItemsSource = entry.Meanings;
            NotFoundText.Visibility = Visibility.Collapsed;
        }
        else
        {
            WordSubtitle.Text = "";
            MeaningsRepeater.ItemsSource = null;
            NotFoundText.Text = entry.Word + " is not in the original 1996 Sakhr search index (usually a rare inflection or a proper noun).";
            NotFoundText.Visibility = Visibility.Visible;
        }
    }

    void ResetPane()
    {
        WordTitle.Text = "قاموس صخر";
        WordSubtitle.Text = "Fluent Sakhr Dictionary - 59,427 lemmas from the original 1996 Sakhr English-Arabic dictionary";
        MeaningsRepeater.ItemsSource = null;
        NotFoundText.Visibility = Visibility.Collapsed;
    }
}
