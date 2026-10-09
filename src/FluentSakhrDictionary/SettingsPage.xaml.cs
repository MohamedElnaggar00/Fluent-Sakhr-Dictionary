using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluentSakhrDictionary;

public partial class SettingsPage : Page
{
    static readonly string[] AccentSwatches = { "#00A6A6", "#0078D4", "#5B5FC7", "#8764B8", "#C239B3", "#E81123", "#F7630C", "#FFB900", "#10893E", "#69797E" };

    readonly Settings _settings;
    bool _ready;

    public SettingsPage()
    {
        InitializeComponent();
        _settings = Settings.Load();
        ThemePicker.SelectedIndex = _settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        BackdropPicker.SelectedIndex = _settings.Backdrop switch { "MicaAlt" => 1, "Acrylic" => 2, _ => 0 };
        InitAccentSwatches();
        WiktionaryToggle.IsOn = _settings.UseWiktionary;
        LanguagePicker.SelectedIndex = _settings.Language switch { "en" => 1, "ar" => 2, _ => 0 };
        LocalizeStatic();
        _ready = true;
    }

    void LocalizeStatic()
    {
        HeaderText.Text = Loc.T("Settings", "الإعدادات");
        LanguageHeader.Text = Loc.T("Language", "اللغة");
        LangSystem.Content = Loc.T("Match device (default)", "مطابق للغة الجهاز (الافتراضي)");
        ThemeHeader.Text = Loc.T("Theme", "السمة");
        ThemeSystem.Content = Loc.T("Use system setting", "مطابق لإعدادات الجهاز");
        ThemeLight.Content = Loc.T("Light", "فاتحة");
        ThemeDark.Content = Loc.T("Dark", "داكنة");
        AccentHeader.Text = Loc.T("Accent color", "اللون المميز");
        AccentCustomButton.Content = Loc.T("Custom color...", "لون مخصص...");
        AccentResetButton.Content = Loc.T("Reset to default", "إعادة للافتراضي");
        DictionaryHeader.Text = Loc.T("Dictionary", "القاموس");
        WiktionaryToggle.Header = Loc.T("Wiktionary database", "قاعدة بيانات Wiktionary");
        WiktionaryToggle.OnContent = Loc.T("On", "مُفعَّل");
        WiktionaryToggle.OffContent = Loc.T("Off", "مُعطَّل");
        WiktionaryNote.Text = Loc.T(
            "Added so the dictionary can understand word inflections (e.g. ABANDONS <- Abandon, يكتبون <- كتب). When off, the app uses the original 1996 Sakhr database only, English to Arabic and back.",
            "أُضيفت قاعدة بيانات Wiktionary ليتمكن القاموس من فهم تصريفات الكلمات (مثل ABANDONS ← Abandon ويكتبون ← كتب). عند إيقاف هذا الخيار يعمل التطبيق بقاعدة بيانات صخر الأصلية لعام 1996 فقط، من الإنجليزية إلى العربية ومن العربية إلى الإنجليزية.");
        BackdropHeader.Text = Loc.T("Backdrop", "الخلفية");
        BackdropMica.Content = "Mica";
        BackdropMicaAlt.Content = "Mica Alt";
        BackdropAcrylic.Content = Loc.T("Desktop Acrylic", "أكريليك سطح المكتب");
    }

    static bool _applyingLanguage; // re-navigation rebuilds this page; never recurse
    void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _applyingLanguage) return;
        _applyingLanguage = true;
        var lang = (LanguagePicker.SelectedItem as RadioButton)?.Tag as string ?? "system";
        if (lang == _settings.Language) { _applyingLanguage = false; return; } // spurious load-time event
        _settings.Language = lang;
        _settings.Save();
        Loc.Apply(_settings);
        if (App.Main is MainWindow w)
        {
            w.ApplyLanguage();
            w.RefreshCurrentPage();
        }
        _applyingLanguage = false;
    }

    void Wiktionary_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (WiktionaryToggle.IsOn == _settings.UseWiktionary) return; // spurious load-time event
        _settings.UseWiktionary = WiktionaryToggle.IsOn;
        _settings.Save();
    }

    void Setting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var theme = (ThemePicker.SelectedItem as RadioButton)?.Tag as string ?? "Default";
        var backdrop = (BackdropPicker.SelectedItem as RadioButton)?.Tag as string ?? "Mica";
        if (theme == _settings.Theme && backdrop == _settings.Backdrop) return; // spurious load-time event
        _settings.Theme = theme;
        _settings.Backdrop = backdrop;
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
            Title = Loc.T("Custom accent color", "لون مميز مخصص"),
            Content = new ScrollViewer { Content = picker, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto },
            CloseButtonText = Loc.T("Done", "تم"),
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


}
