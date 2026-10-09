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
        Title = "Fluent Sakhr Dictionary";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1024, 680));
        ApplyTheme(_settings.Theme);
        ApplyBackdrop(_settings.Backdrop);
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
