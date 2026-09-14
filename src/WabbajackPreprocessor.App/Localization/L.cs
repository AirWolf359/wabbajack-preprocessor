using System.Globalization;
using System.Resources;

namespace WabbajackPreprocessor.App.Localization;

/// <summary>
/// Localization access. XAML binds through the singleton's indexer
/// (<c>{Binding [Key], Source={x:Static loc:L.Instance}}</c>); code uses
/// <see cref="Get"/> and <see cref="F"/>. The UI culture is set once at startup
/// (from preferences or the OS), so lookups are stable for the process lifetime;
/// changing the language takes effect on restart.
/// </summary>
public sealed class L
{
    public static L Instance { get; } = new();

    private static readonly ResourceManager Rm =
        new("WabbajackPreprocessor.App.Localization.Strings", typeof(L).Assembly);

    public string this[string key] => Get(key);

    public static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
