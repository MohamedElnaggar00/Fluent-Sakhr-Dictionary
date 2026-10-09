using System.Text.Json;

namespace FluentSakhrDictionary;

/// <summary>Persisted user settings in %APPDATA%/FluentSakhrDictionary/settings.json.</summary>
public class Settings
{
    public string Theme { get; set; } = "Default";   // Default | Light | Dark
    public string Backdrop { get; set; } = "Mica";   // Mica | MicaAlt | Acrylic
    public string Accent { get; set; } = "";        // "" = default teal #00A6A6
    public bool UseWiktionary { get; set; } = true; // false = original 1996 Sakhr database only

    static string FilePath => Path.Combine(App.AppData, "settings.json");

    public static Settings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { }
        return new();
    }

    public void Save()
    {
        try { Directory.CreateDirectory(App.AppData); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); } catch { }
    }
}
