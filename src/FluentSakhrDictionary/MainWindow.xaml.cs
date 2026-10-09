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
        ContentFrame.Navigate(typeof(SearchPage));
        Activated += (_, _) => { if (ContentFrame.Content is SearchPage sp) sp.FocusSearchBox(); };
        if (App.ScreenshotPath != null) RunScreenshot();
    }

    async void RunScreenshot()
    {
        void Log(string line) { try { Directory.CreateDirectory(App.AppData); File.AppendAllText(Path.Combine(App.AppData, "crash.log"), DateTime.Now + " SHOT " + line + "\n"); } catch { } }
        try
        {
            Log("start theme=" + App.ShotTheme + " word=" + App.ShotWord + " path=" + App.ScreenshotPath);
            if (App.ShotTheme != null) ApplyTheme(App.ShotTheme);
            await Task.Delay(1200);
            if (App.ShotPage == "settings") { ContentFrame.Navigate(typeof(SettingsPage)); Log("settings page"); }
            if (App.ShotWord != null && ContentFrame.Content is SearchPage sp) { sp.TypeAndSelect(App.ShotWord); Log("word typed"); }
            await Task.Delay(1500);
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Log("hwnd=" + hwnd);
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
        ContentFrame.Navigate(page, null, new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo());
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
