namespace WabbajackPreprocessor.Core.Mo2;

/// <summary>
/// Minimal, tolerant reader for Qt QSettings-style INI files as written by Mod Organizer 2
/// (ModOrganizer.ini, mods\*\meta.ini, downloads\*.meta). Handles @ByteArray(...) wrappers,
/// backslash escapes in values, percent-encoded keys, and duplicate keys/sections (last wins).
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public const string DefaultSection = "General";

    public string? Get(string section, string key) =>
        _sections.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) ? v : null;

    public IReadOnlyDictionary<string, string>? GetSection(string section) =>
        _sections.TryGetValue(section, out var s) ? s : null;

    public static IniFile? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static IniFile Parse(string text)
    {
        var ini = new IniFile();
        var current = DefaultSection;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line[0] is ';' or '#')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                current = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 1)
                continue;

            var key = DecodeKey(line[..eq].Trim());
            var value = DecodeValue(line[(eq + 1)..].Trim());
            if (!ini._sections.TryGetValue(current, out var section))
                ini._sections[current] = section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            section[key] = value;
        }
        return ini;
    }

    private static string DecodeKey(string key)
    {
        // QSettings percent-encodes special characters in keys (e.g. %20 for space).
        return key.Contains('%') ? Uri.UnescapeDataString(key) : key;
    }

    private static string DecodeValue(string value)
    {
        if (value.StartsWith("@ByteArray(", StringComparison.Ordinal) && value.EndsWith(')'))
            value = value[11..^1];
        else if (value.StartsWith("@Variant(", StringComparison.Ordinal))
            return value; // opaque Qt-serialized value; keep as-is

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];

        if (!value.Contains('\\'))
            return value;

        var sb = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                sb.Append(value[i]);
                continue;
            }
            i++;
            sb.Append(value[i] switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '0' => '\0',
                _ => value[i], // covers \\ -> \ and \" -> "
            });
        }
        return sb.ToString();
    }
}
