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
    private string _profilesSummary = "";

    [ObservableProperty]
    private string _statusMessage = "Select a .compiler_settings file and click Analyze.";

    [ObservableProperty]
    private string _staleHeader = "Stale entries";

    [ObservableProperty]
    private string _redundantHeader = "Redundant Always Enabled";

    [ObservableProperty]
    private string _disabledHeader = "Disabled mods";

    [ObservableProperty]
    private string _downloadsHeader = "No download";

    [ObservableProperty]
    private string _warningsHeader = "Warnings";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _hasAnalysis;

    public ObservableCollection<StaleGroup> StaleGroups { get; } = [];
    public ObservableCollection<StaleEntryItem> RedundantItems { get; } = [];
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

            StaleGroups.Clear();
            foreach (var group in result.StaleEntries.GroupBy(f => f.List).OrderBy(g => g.Key))
                StaleGroups.Add(new StaleGroup(group.Key, group.Select(f => new StaleEntryItem(f)).ToList()));
            var staleCount = StaleGroups.Sum(g => g.Items.Count);

            RedundantItems.Clear();
            foreach (var finding in result.RedundantAlwaysEnabled)
                RedundantItems.Add(new StaleEntryItem(finding));

            DisabledItems.Clear();
            foreach (var finding in result.DisabledMods)
                DisabledItems.Add(new DisabledModItem(finding));

            DownloadItems.Clear();
            foreach (var finding in result.ModsWithoutDownload)
                DownloadItems.Add(new MissingDownloadItem(finding));

            Warnings.Clear();
            foreach (var warning in result.Warnings)
                Warnings.Add(warning);

            StaleHeader = $"Stale entries ({staleCount})";
            RedundantHeader = $"Redundant Always Enabled ({RedundantItems.Count})";
            DisabledHeader = $"Disabled mods ({DisabledItems.Count})";
            DownloadsHeader = $"No download ({DownloadItems.Count})";
            WarningsHeader = $"Warnings ({Warnings.Count})";

            InstanceSummary =
                $"{_settings.ModListName} — source: {source} — " +
                $"{instance.Mods.Count} mods, {instance.Downloads.Count} downloads";

            // Show what the settings file specifies, flagging profiles missing on disk.
            string Describe(string? name) =>
                string.IsNullOrWhiteSpace(name) ? "(none set)"
                : instance.Profiles.ContainsKey(name) ? name
                : $"{name} (not found on disk!)";
            var additional = _settings.AdditionalProfiles.Count > 0
                ? string.Join(", ", _settings.AdditionalProfiles.Select(Describe))
                : "none";
            ProfilesSummary =
                $"Default profile: {Describe(_settings.Profile)}   •   Additional profiles: {additional}";

            HasAnalysis = true;
            var total = staleCount + RedundantItems.Count + DisabledItems.Count + DownloadItems.Count;
            StatusMessage = total == 0
                ? "Analysis complete: nothing to clean up."
                : $"Analysis complete: {staleCount} stale entries, " +
                  $"{RedundantItems.Count} redundant Always Enabled tags, " +
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
        plan.RemoveEntries.AddRange(
            StaleGroups.SelectMany(g => g.Items).Where(i => i.Remove).Select(i => i.Finding));
        plan.RemoveEntries.AddRange(RedundantItems.Where(i => i.Remove).Select(i => i.Finding));
        plan.MarkAlwaysEnabled.AddRange(DisabledItems.Where(i => i.MarkAlwaysEnabled).Select(i => i.ModName));
        foreach (var item in DownloadItems.Where(i => i.IsChanged))
        {
            if (item.Finding.TaggedInclude && item.SelectedActionIndex != 1)
                plan.UntagInclude.Add(item.ModName);
            if (item.Finding.TaggedNoMatchInclude && item.SelectedActionIndex != 2)
                plan.UntagNoMatchInclude.Add(item.ModName);
            if (!item.Finding.TaggedInclude && item.SelectedActionIndex == 1)
                plan.MarkInclude.Add(item.ModName);
            if (!item.Finding.TaggedNoMatchInclude && item.SelectedActionIndex == 2)
                plan.MarkNoMatchInclude.Add(item.ModName);
        }

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
