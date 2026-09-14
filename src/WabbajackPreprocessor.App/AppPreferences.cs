using System.Text.Json;

namespace WabbajackPreprocessor.App;

/// <summary>
/// Small persisted app preferences (theme, later language), stored at
/// %APPDATA%\WabbajackPreprocessor\preferences.json. All operations are
/// best-effort: a missing or corrupt file just yields defaults.
/// </summary>
public class AppPreferences
{
    /// <summary>"System", "Dark", or "Light".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>UI culture name (e.g. "de"); null follows the OS language.</summary>
    public string? Language { get; set; }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WabbajackPreprocessor", "preferences.json");

    public static AppPreferences Instance { get; } = Load();

    private static AppPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(FilePath))
                       ?? new AppPreferences();
        }
        catch (Exception)
        {
            // fall through to defaults
        }
        return new AppPreferences();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // preferences are a convenience; never crash over them
        }
    }
}
