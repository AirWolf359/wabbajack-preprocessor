using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WabbajackPreprocessor.Core.Analysis;
using WabbajackPreprocessor.Core.Mo2;
using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private CompilerSettings? _settings;
    private string? _loadedSettingsPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string _settingsPath = "";

    [ObservableProperty]
    private string _instanceSummary = "";

    [ObservableProperty]
    private string _statusMessage = "Select a .compiler_settings file and click Analyze.";

    [ObservableProperty]
    private string _staleHeader = "Stale entries";

    [ObservableProperty]
    private string _disabledHeader = "Disabled mods";

    [ObservableProperty]
    private string _downloadsHeader = "No download";

    [ObservableProperty]
    private string _warningsHeader = "Warnings";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _hasAnalysis;

    public ObservableCollection<StaleEntryItem> StaleItems { get; } = [];
    public ObservableCollection<DisabledModItem> DisabledItems { get; } = [];
    public ObservableCollection<MissingDownloadItem> DownloadItems { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    private bool CanAnalyze() => !string.IsNullOrWhiteSpace(SettingsPath);

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private void Analyze()
    {
        try
        {
            var path = SettingsPath.Trim().Trim('"');
            if (!File.Exists(path))
            {
                StatusMessage = $"File not found: {path}";
                return;
            }

            _settings = CompilerSettingsFile.Load(path);
            _loadedSettingsPath = path;

            // The settings file normally sits in the instance root; prefer its recorded
            // Source when that folder still exists, else fall back to the file's folder.
            var source = _settings.Source;
            if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
                source = Path.GetDirectoryName(Path.GetFullPath(path))!;

            var downloads = _settings.Downloads;
            if (string.IsNullOrWhiteSpace(downloads) || !Directory.Exists(downloads))
                downloads = null; // let Mo2Instance fall back to ModOrganizer.ini / <source>\downloads

            var instance = Mo2Instance.Load(source, downloads);
            var result = Analyzer.Analyze(_settings, instance);

            StaleItems.Clear();
            foreach (var finding in result.StaleEntries)
                StaleItems.Add(new StaleEntryItem(finding));

            DisabledItems.Clear();
            foreach (var finding in result.DisabledMods)
                DisabledItems.Add(new DisabledModItem(finding));

            DownloadItems.Clear();
            foreach (var finding in result.ModsWithoutDownload)
                DownloadItems.Add(new MissingDownloadItem(finding));

            Warnings.Clear();
            foreach (var warning in result.Warnings)
                Warnings.Add(warning);

            StaleHeader = $"Stale entries ({StaleItems.Count})";
            DisabledHeader = $"Disabled mods ({DisabledItems.Count})";
            DownloadsHeader = $"No download ({DownloadItems.Count})";
            WarningsHeader = $"Warnings ({Warnings.Count})";

            var profiles = result.ProfilesUsed.Count > 0
                ? string.Join(", ", result.ProfilesUsed)
                : "(none found)";
            InstanceSummary =
                $"{_settings.ModListName} — source: {source} — profiles: {profiles} — " +
                $"{instance.Mods.Count} mods, {instance.Downloads.Count} downloads";

            HasAnalysis = true;
            var total = StaleItems.Count + DisabledItems.Count + DownloadItems.Count;
            StatusMessage = total == 0
                ? "Analysis complete: nothing to clean up."
                : $"Analysis complete: {StaleItems.Count} stale entries, " +
                  $"{DisabledItems.Count} disabled mods, {DownloadItems.Count} mods without a download.";
        }
        catch (Exception ex)
        {
            HasAnalysis = false;
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
    }

    private bool CanApply() => HasAnalysis;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (_settings is null || _loadedSettingsPath is null)
            return;

        var plan = new SettingsUpdatePlan();
        plan.RemoveEntries.AddRange(StaleItems.Where(i => i.Remove).Select(i => i.Finding));
        plan.MarkAlwaysEnabled.AddRange(DisabledItems.Where(i => i.MarkAlwaysEnabled).Select(i => i.ModName));
        plan.MarkInclude.AddRange(DownloadItems.Where(i => i.SelectedActionIndex == 1).Select(i => i.ModName));
        plan.MarkNoMatchInclude.AddRange(DownloadItems.Where(i => i.SelectedActionIndex == 2).Select(i => i.ModName));

        if (plan.IsEmpty)
        {
            StatusMessage = "No changes selected.";
            return;
        }

        try
        {
            var changes = SettingsUpdater.Apply(_settings, plan);
            var backupPath = CompilerSettingsFile.Save(_loadedSettingsPath, _settings);

            // Refresh the lists against the file we just wrote, then report the save
            // (Analyze() sets its own status message).
            Analyze();
            StatusMessage = backupPath is null
                ? $"Saved {changes.Count} change(s) to {Path.GetFileName(_loadedSettingsPath)}."
                : $"Saved {changes.Count} change(s). Backup: {Path.GetFileName(backupPath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }
}
