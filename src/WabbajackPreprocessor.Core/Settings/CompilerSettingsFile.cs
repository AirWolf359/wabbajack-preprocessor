using System.Text.Json;

namespace WabbajackPreprocessor.Core.Settings;

/// <summary>Loads and saves <c>*.compiler_settings</c> files with Wabbajack-compatible JSON options.</summary>
public static class CompilerSettingsFile
{
    // Mirrors Wabbajack's DTOSerializer: indented output, tolerant reader
    // (comments, trailing commas, numbers-as-strings), default (\uXXXX) escaping.
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public static CompilerSettings Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<CompilerSettings>(stream, Options)
               ?? throw new InvalidDataException($"'{path}' does not contain a JSON object.");
    }

    /// <summary>
    /// Saves the settings, first copying the existing file (if any) to a timestamped
    /// <c>.bak</c> alongside it. Returns the backup path, or null if no backup was made.
    /// </summary>
    public static string? Save(string path, CompilerSettings settings, bool backup = true)
    {
        string? backupPath = null;
        if (backup && File.Exists(path))
        {
            backupPath = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, backupPath, overwrite: true);
        }

        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(path, json);
        return backupPath;
    }
}
