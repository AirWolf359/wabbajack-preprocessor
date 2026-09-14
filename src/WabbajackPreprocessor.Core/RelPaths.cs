namespace WabbajackPreprocessor.Core;

/// <summary>
/// Helpers for Wabbajack-style relative paths: backslash-separated, compared
/// case-insensitively and segment-wise (an entry "mods\Foo" covers "mods\Foo\bar.esp"
/// but not "mods\FooBar").
/// </summary>
public static class RelPaths
{
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static readonly char[] Separators = ['/', '\\'];

    public static string[] Split(string path) =>
        path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Canonical form: backslash separators, no empty segments.</summary>
    public static string Normalize(string path) => string.Join('\\', Split(path));

    public static bool AreEqual(string a, string b) =>
        Comparer.Equals(Normalize(a), Normalize(b));

    /// <summary>
    /// True when <paramref name="path"/> equals <paramref name="folder"/> or lies under it,
    /// comparing whole segments case-insensitively.
    /// </summary>
    public static bool IsSameOrUnder(string path, string folder)
    {
        var pathParts = Split(path);
        var folderParts = Split(folder);
        if (folderParts.Length == 0 || folderParts.Length > pathParts.Length)
            return false;
        for (var i = 0; i < folderParts.Length; i++)
        {
            if (!Comparer.Equals(pathParts[i], folderParts[i]))
                return false;
        }
        return true;
    }

    /// <summary>True when any entry in <paramref name="entries"/> covers <paramref name="path"/>.</summary>
    public static bool IsCoveredBy(string path, IEnumerable<string> entries) =>
        entries.Any(e => IsSameOrUnder(path, e));
}
