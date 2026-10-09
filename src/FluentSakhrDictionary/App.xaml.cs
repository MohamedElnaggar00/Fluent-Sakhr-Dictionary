using Microsoft.UI.Xaml;

namespace FluentSakhrDictionary;

public partial class App : Application
{
    public static string AppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentSakhrDictionary");
    public static MainWindow? Main;
    /// <summary>CI-only: --screenshot=path [--shot-word=CAT] [--theme=Dark] renders the app and exits.</summary>
    public static string? ScreenshotPath, ShotWord, ShotTheme;
    static Mutex? _mutex;
    MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            try { Directory.CreateDirectory(AppData); File.AppendAllText(Path.Combine(AppData, "crash.log"), DateTime.Now + "\n" + e.Exception + "\n\n"); } catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var argv = Environment.GetCommandLineArgs();
        for (int i = 0; i < argv.Length; i++)
        {
            var a = argv[i];
            if (a.StartsWith("--screenshot=")) ScreenshotPath = a[13..];
            else if (a == "--screenshot" && i + 1 < argv.Length) ScreenshotPath = argv[++i];
            if (a.StartsWith("--shot-word=")) ShotWord = a[12..];
            else if (a == "--shot-word" && i + 1 < argv.Length) ShotWord = argv[++i];
            if (a.StartsWith("--theme=")) ShotTheme = a[8..];
            else if (a == "--theme" && i + 1 < argv.Length) ShotTheme = argv[++i];
        }
        _mutex = new Mutex(true, "FluentSakhrDictionary.SingleInstance", out bool created);
        if (!created) { Environment.Exit(0); return; }
        _window = new MainWindow();
        Main = _window;
        _window.Activate();
    }
}
