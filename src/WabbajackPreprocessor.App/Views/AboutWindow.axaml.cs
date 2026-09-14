using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WabbajackPreprocessor.App.Views;

public partial class AboutWindow : Window
{
    public const string RepoUrl = "https://github.com/AirWolf359/wabbajack-preprocessor";

    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        // Strip any source-revision suffix (e.g. "1.2.3+abcdef")
        var plus = version.IndexOf('+');
        if (plus > 0)
            version = version[..plus];
        VersionText.Text = Localization.L.F("AboutVersionFmt", version);
    }

    private async void OnRepoClicked(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(RepoUrl));

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
