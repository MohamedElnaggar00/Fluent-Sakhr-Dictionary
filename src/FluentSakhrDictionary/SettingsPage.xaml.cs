using Microsoft.UI.Xaml.Controls;

namespace FluentSakhrDictionary;

public partial class SettingsPage : Page
{
    readonly Settings _settings;
    bool _ready;

    public SettingsPage()
    {
        InitializeComponent();
        _settings = Settings.Load();
        ThemePicker.SelectedIndex = _settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        BackdropPicker.SelectedIndex = _settings.Backdrop switch { "MicaAlt" => 1, "Acrylic" => 2, _ => 0 };
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
}
