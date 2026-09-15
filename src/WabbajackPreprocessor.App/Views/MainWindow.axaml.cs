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
        DataContextChanged += OnDataContextChangedHookCounts;
    }

    // A header content size change does not re-measure the tab strip on its own, so a
    // badge outgrowing its reserved width would overflow under the neighboring tab.
    // Explicitly re-measure the tabs whenever the analysis counts change.
    private void OnDataContextChangedHookCounts(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.StaleCount)
                or nameof(MainViewModel.RedundantCount)
                or nameof(MainViewModel.DisabledCount)
                or nameof(MainViewModel.DownloadCount)
                or nameof(MainViewModel.WarningCount)
                or nameof(MainViewModel.HasAnalysis))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(RemeasureTabs,
                    Avalonia.Threading.DispatcherPriority.Background);
            }
        };
    }

    private void RemeasureTabs()
    {
        foreach (var tab in Tabs.Items.OfType<TabItem>())
            tab.InvalidateMeasure();
        (Tabs.Presenter?.Panel as Control)?.InvalidateMeasure();
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
            Title = Localization.L.Get("FilePickerTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Localization.L.Get("FilePickerFilterName"))
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
