using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WabbajackPreprocessor.App.ViewModels;

namespace WabbajackPreprocessor.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
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
