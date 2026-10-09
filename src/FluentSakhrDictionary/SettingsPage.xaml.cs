using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluentSakhrDictionary;

public partial class SettingsPage : Page
{
    static readonly string[] AccentSwatches = { "#00A6A6", "#0078D4", "#5B5FC7", "#8764B8", "#C239B3", "#E81123", "#F7630C", "#FFB900", "#10893E", "#69797E" };
    static string CurrentVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    readonly Settings _settings;
    bool _ready;

    public SettingsPage()
    {
        InitializeComponent();
        _settings = Settings.Load();
        ThemePicker.SelectedIndex = _settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        BackdropPicker.SelectedIndex = _settings.Backdrop switch { "MicaAlt" => 1, "Acrylic" => 2, _ => 0 };
        InitAccentSwatches();
        _ready = true;
    }

    void Setting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _settings.Theme = (ThemePicker.SelectedItem as RadioButton)?.Tag as string ?? "Default";
        _settings.Backdrop = (BackdropPicker.SelectedItem as RadioButton)?.Tag as string ?? "Mica";
        _settings.Save();
        if (App.Main is MainWindow w)
        {
            w.ApplyTheme(_settings.Theme);
            w.ApplyBackdrop(_settings.Backdrop);
        }
    }

    void InitAccentSwatches()
    {
        int n = 0;
        foreach (var hex in AccentSwatches)
        {
            var color = MainWindow.ParseAccent(hex);
            var button = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                Tag = color,
            };
            button.Click += (s, e) => { if (App.Main is MainWindow w) w.SetAccent((Windows.UI.Color)((Button)s).Tag, save: true); };
            (n++ < 5 ? AccentRow1 : AccentRow2).Children.Add(button);
        }
    }

    async void AccentCustom_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPicker
        {
            Color = MainWindow.ParseAccent(_settings.Accent),
            IsAlphaEnabled = false,
            IsMoreButtonVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
        };
        picker.ColorChanged += (s2, a2) => { if (App.Main is MainWindow w) w.SetAccent(a2.NewColor, save: true); };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Custom accent color",
            Content = new ScrollViewer { Content = picker, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto },
            CloseButtonText = "Done",
            DefaultButton = ContentDialogButton.Close,
        };
        try { await dialog.ShowAsync(); } catch { }
    }

    void AccentReset_Click(object sender, RoutedEventArgs e)
    {
        if (App.Main is MainWindow w) w.SetAccent(MainWindow.ParseAccent(MainWindow.DefaultAccentHex), save: true);
        _settings.Accent = "";
        _settings.Save();
    }

    /// <summary>Manual update check: fires only from this button, never in the background.</summary>
    async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        UpdateLink.Visibility = Visibility.Collapsed;
        UpdateResult.Text = "Checking...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SakhrDictionaryRevive/" + CurrentVersion);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            // releases?per_page=1 rather than /latest: our releases are marked pre-release, which /latest skips.
            using var response = await http.GetAsync("https://api.github.com/repos/MohamedElnaggar00/Sakhr-Dictionary-Revive/releases?per_page=1");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string tag = "";
            foreach (var release in json.RootElement.EnumerateArray())
            {
                tag = (release.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                break;
            }
            if (Version.TryParse(tag, out var latest) && Version.TryParse(CurrentVersion, out var current) && latest > current)
            {
                UpdateResult.Text = "Version " + tag + " is available.";
                UpdateLink.NavigateUri = new Uri("https://github.com/MohamedElnaggar00/Sakhr-Dictionary-Revive/releases/tag/v" + tag);
                UpdateLink.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateResult.Text = tag.Length > 0
                    ? "You're up to date (v" + CurrentVersion + ")."
                    : "No releases found yet.";
            }
        }
        catch
        {
            UpdateResult.Text = "Couldn't check for updates. Check your internet connection and try again.";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }
}
