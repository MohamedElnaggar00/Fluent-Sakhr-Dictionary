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
        foreach (var a in Environment.GetCommandLineArgs())
        {
            if (a.StartsWith("--screenshot=")) ScreenshotPath = a[13..];
            if (a.StartsWith("--shot-word=")) ShotWord = a[12..];
            if (a.StartsWith("--theme=")) ShotTheme = a[8..];
        }
        _mutex = new Mutex(true, "FluentSakhrDictionary.SingleInstance", out bool created);
        if (!created) { Environment.Exit(0); return; }
        _window = new MainWindow();
        Main = _window;
        _window.Activate();
    }
}
