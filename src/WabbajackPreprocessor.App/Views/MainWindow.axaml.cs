using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using WabbajackPreprocessor.App.ViewModels;

namespace WabbajackPreprocessor.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ExitCommand = new RelayCommand(Close);
        OpenSettingsCommand = new AsyncRelayCommand(() =>
            new SettingsWindow { DataContext = DataContext }.ShowDialog(this));
        OpenAboutCommand = new AsyncRelayCommand(() =>
            new AboutWindow().ShowDialog(this));
        OpenRepoCommand = new AsyncRelayCommand(async () =>
            await Launcher.LaunchUriAsync(new Uri(AboutWindow.RepoUrl)));
        InitializeComponent();
    }

    public IAsyncRelayCommand BrowseCommand { get; }
    public IRelayCommand ExitCommand { get; }
    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenAboutCommand { get; }
    public IAsyncRelayCommand OpenRepoCommand { get; }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e) => await BrowseAsync();

    private async Task BrowseAsync()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a compiler settings file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Wabbajack compiler settings")
                {
                    Patterns = ["*.compiler_settings"],
                },
                FilePickerFileTypes.All,
            ],
        });

        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path)
        {
            vm.SettingsPath = path;
            if (vm.AnalyzeCommand.CanExecute(null))
                vm.AnalyzeCommand.Execute(null);
        }
    }
}
