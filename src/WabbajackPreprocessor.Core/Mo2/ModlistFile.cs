namespace WabbajackPreprocessor.Core.Mo2;

public enum ModlistEntryKind
{
    /// <summary>Line prefixed with <c>+</c>.</summary>
    Enabled,
    /// <summary>Line prefixed with <c>-</c>.</summary>
    Disabled,
    /// <summary>Line prefixed with <c>*</c>: unmanaged/foreign entry with no mods\ folder.</summary>
    Foreign,
}

/// <summary>One mod line of a profile's modlist.txt (first line = highest priority).</summary>
public sealed record ModlistEntry(string Name, ModlistEntryKind Kind)
{
    public bool IsSeparator => Name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether Wabbajack's compiler treats this line as enabled: <c>+</c> lines and
    /// separator lines (which MO2 writes with <c>-</c>).
    /// </summary>
    public bool WabbajackEnabled => Kind == ModlistEntryKind.Enabled || IsSeparator;
}

public static class ModlistFile
{
    public static List<ModlistEntry> Load(string path) => Parse(File.ReadLines(path));

    public static List<ModlistEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<ModlistEntry>();
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 2 || line.StartsWith('#'))
                continue;

            ModlistEntryKind kind;
            switch (line[0])
            {
                case '+': kind = ModlistEntryKind.Enabled; break;
                case '-': kind = ModlistEntryKind.Disabled; break;
                case '*': kind = ModlistEntryKind.Foreign; break;
                default: continue;
            }
            var name = line[1..].Trim();
            if (name.Length > 0)
                entries.Add(new ModlistEntry(name, kind));
        }
        return entries;
    }
}
