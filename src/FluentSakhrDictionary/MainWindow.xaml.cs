using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace FluentSakhrDictionary;

public partial class MainWindow : Window
{
    readonly Settings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();
        _ = DictionaryService.CoreReady; // warm the data load while the window comes up
        Loc.Apply(_settings);
        Title = "Sakhr Dictionary Revive - قاموس صخر الحديث";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700));
        // Taskbar/Alt-Tab icon: unpackaged apps do not inherit the exe icon on the window.
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico")); } catch { }
        ApplyTheme(_settings.Theme);
        ApplyBackdrop(_settings.Backdrop);
        SetAccent(ParseAccent(_settings.Accent), save: false);
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowChrome.Apply(hwnd);
        }
        catch { }
        ApplyLanguage();
        ContentFrame.Navigate(typeof(SearchPage));
        Activated += (_, _) =>
        {
            // DWM attributes can be reset by DPI changes and restore/maximize transitions;
            // reapply the border suppression and RTL mirror so the frame stays clean.
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowChrome.Apply(hwnd);
                WindowChrome.SetRtlMirror(hwnd, Loc.IsArabic);
            }
            catch { }
            if (ContentFrame.Content is SearchPage sp) sp.FocusSearchBox();
        };
        if (App.ScreenshotPath != null) RunScreenshot();
    }

    /// <summary>Applies the display language: mirrors the whole layout for Arabic and
    /// localizes the navigation labels. Pages localize when (re)navigated.</summary>
    public void ApplyLanguage()
    {
        Root.FlowDirection = Loc.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        try { WindowChrome.SetRtlMirror(WinRT.Interop.WindowNative.GetWindowHandle(this), Loc.IsArabic); } catch { }
        NavSearch.Content = Loc.T("Search", "بحث");
        NavSettings.Content = Loc.T("Settings", "الإعدادات");
        NavAbout.Content = Loc.T("About", "حول");
    }

    /// <summary>Re-navigates to the current page so it re-renders in the new language.</summary>
    public void RefreshCurrentPage()
    {
        if (ContentFrame.Content != null) ContentFrame.Navigate(ContentFrame.Content.GetType());
    }

    async void RunScreenshot()
    {
        void Log(string line) { try { Directory.CreateDirectory(App.AppData); File.AppendAllText(Path.Combine(App.AppData, "crash.log"), DateTime.Now + " SHOT " + line + "\n"); } catch { } }
        try
        {
            Log("start theme=" + App.ShotTheme + " word=" + App.ShotWord + " path=" + App.ScreenshotPath);
            if (App.ShotTheme != null) ApplyTheme(App.ShotTheme);
            if (App.ShotWidth > 0) { AppWindow.Resize(new Windows.Graphics.SizeInt32(App.ShotWidth, App.ShotHeight)); Log("resized " + App.ShotWidth + "x" + App.ShotHeight); }
            await Task.Delay(1200);
            if (App.ShotPage == "settings") { ContentFrame.Navigate(typeof(SettingsPage)); Log("settings page"); }
            if (App.ShotPage == "settings-nav") { Nav.SelectedItem = NavSettings; Log("settings page via nav transition"); }
            if (App.ShotPage == "about") { Nav.SelectedItem = NavAbout; Log("about page via nav transition"); }
            if (App.ShotWord != null && ContentFrame.Content is SearchPage sp) { await sp.TypeAndSelect(App.ShotWord); Log("word typed"); }
            await Task.Delay(1500);
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Log("hwnd=" + hwnd);
            // WS_EX_LAYOUTRTL mirrors the capture DC itself (mirrored-glyph screenshots); drop it for
            // the capture only - the real on-screen mirror is unaffected and this process exits right after.
            try { WindowChrome.SetRtlMirror(hwnd, false); await Task.Delay(250); } catch { }
            CaptureHelper.Save(hwnd, App.ScreenshotPath!);
            Log("saved");
        }
        catch (Exception ex) { Log("FAILED " + ex); }
        Environment.Exit(0);
    }

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        Type page = item.Tag as string switch
        {
            "settings" => typeof(SettingsPage),
            "about" => typeof(AboutPage),
            _ => typeof(SearchPage),
        };
        // Standard entrance animation: lighter than DrillIn (which froze mid-animation on his
        // 125% custom-scale machine) while keeping navigation animated (his standing choice).
        ContentFrame.Navigate(page, null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
    }

    // ---------- accent color (same pattern as Fluent Prayer Times / Fluent Vantage Toolbar) ----------
    public const string DefaultAccentHex = "#00A6A6";

    public static Windows.UI.Color ParseAccent(string? hex)
    {
        try
        {
            var h = (hex ?? "").TrimStart('#');
            if (h.Length == 6) return Windows.UI.Color.FromArgb(255, Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..], 16));
        }
        catch { }
        return Windows.UI.Color.FromArgb(255, 0x00, 0xA6, 0xA6);
    }

    static Windows.UI.Color Mix(Windows.UI.Color c, byte tr, byte tg, byte tb, double t) =>
        Windows.UI.Color.FromArgb(255, (byte)(c.R + (tr - c.R) * t), (byte)(c.G + (tg - c.G) * t), (byte)(c.B + (tb - c.B) * t));

    static void ApplyAccentResources(Windows.UI.Color c)
    {
        var r = Application.Current.Resources;
        r["SystemAccentColor"] = c;
        r["SystemAccentColorDark1"] = Mix(c, 0, 0, 0, 0.18);
        r["SystemAccentColorDark2"] = Mix(c, 0, 0, 0, 0.36);
        r["SystemAccentColorDark3"] = Mix(c, 0, 0, 0, 0.54);
        r["SystemAccentColorLight1"] = Mix(c, 255, 255, 255, 0.18);
        r["SystemAccentColorLight2"] = Mix(c, 255, 255, 255, 0.36);
        r["SystemAccentColorLight3"] = Mix(c, 255, 255, 255, 0.54);
    }

    public void SetAccent(Windows.UI.Color c, bool save)
    {
        ApplyAccentResources(c);
        if (save)
        {
            var s = Settings.Load();
            s.Accent = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            if (s.Accent.Equals(DefaultAccentHex, StringComparison.OrdinalIgnoreCase)) s.Accent = "";
            s.Save();
        }
        RefreshAccent();
    }

    void RefreshAccent()
    {
        // ThemeResource bindings re-resolve on a theme change: flip the element theme and back.
        if (Content is FrameworkElement root)
        {
            var was = root.RequestedTheme;
            root.RequestedTheme = was == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            root.RequestedTheme = was;
        }
    }

    public void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
    }

    public void ApplyBackdrop(string backdrop)
    {
        SystemBackdrop = backdrop switch
        {
            "MicaAlt" => new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt },
            "Acrylic" => new DesktopAcrylicBackdrop(),
            _ => new MicaBackdrop(),
        };
    }
}
