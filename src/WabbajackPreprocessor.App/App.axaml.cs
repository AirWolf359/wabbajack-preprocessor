using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WabbajackPreprocessor.App.ViewModels;
using WabbajackPreprocessor.App.Views;

namespace WabbajackPreprocessor.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel.ApplyTheme(AppPreferences.Instance.Theme);

            var viewModel = new MainViewModel();

            // Allow launching with a settings file: WabbajackPreprocessor <file.compiler_settings>
            var argPath = desktop.Args?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(argPath))
            {
                viewModel.SettingsPath = argPath;
                if (viewModel.AnalyzeCommand.CanExecute(null))
                    viewModel.AnalyzeCommand.Execute(null);
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}