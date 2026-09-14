using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WabbajackPreprocessor.App.Localization;
using WabbajackPreprocessor.Core.Analysis;
using WabbajackPreprocessor.Core.Mo2;
using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private CompilerSettings? _settings;
    private string? _loadedSettingsPath;

    /// <summary>Canonical stored values; index-aligned with <see cref="ThemeDisplayOptions"/>.</summary>
    public static readonly string[] ThemeOptions = ["System", "Dark", "Light"];

    /// <summary>Localized labels shown in the theme dropdown.</summary>
    public static readonly string[] ThemeDisplayOptions =
        [L.Get("ThemeSystem"), L.Get("ThemeDark"), L.Get("ThemeLight")];

    [ObservableProperty]
    private int _selectedThemeIndex = Math.Max(0,
        Array.IndexOf(ThemeOptions, AppPreferences.Instance.Theme));

    partial void OnSelectedThemeIndexChanged(int value)
    {
        ApplyTheme(ThemeOptions[Math.Clamp(value, 0, ThemeOptions.Length - 1)]);
        AppPreferences.Instance.Theme = ThemeOptions[Math.Clamp(value, 0, ThemeOptions.Length - 1)];
        AppPreferences.Instance.Save();
    }

    /// <summary>Sets the application theme variant; "System" follows Windows.</summary>
    public static void ApplyTheme(string theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                "Dark" => ThemeVariant.Dark,
                "Light" => ThemeVariant.Light,
                _ => ThemeVariant.Default,
            };
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string _settingsPath = "";

    [ObservableProperty]
    private string _instanceSummary = "";

    [ObservableProperty]
    private string _profilesSummary = "";

    [ObservableProperty]
    private string _statusMessage = L.Get("StatusInitial");

    [ObservableProperty]
    private string _staleHeader = L.Get("TabStalePlain");

    [ObservableProperty]
    private string _redundantHeader = L.Get("TabRedundantPlain");

    [ObservableProperty]
    private string _disabledHeader = L.Get("TabDisabledPlain");

    [ObservableProperty]
    private string _downloadsHeader = L.Get("TabNoDownloadPlain");

    [ObservableProperty]
    private string _warningsHeader = L.Get("TabWarningsPlain");

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
                StatusMessage = L.F("StatusFileNotFoundFmt", path);
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
                Warnings.Add(FormatWarning(warning));

            StaleHeader = L.F("TabCountFmt", L.Get("TabStalePlain"), staleCount);
            RedundantHeader = L.F("TabCountFmt", L.Get("TabRedundantPlain"), RedundantItems.Count);
            DisabledHeader = L.F("TabCountFmt", L.Get("TabDisabledPlain"), DisabledItems.Count);
            DownloadsHeader = L.F("TabCountFmt", L.Get("TabNoDownloadPlain"), DownloadItems.Count);
            WarningsHeader = L.F("TabCountFmt", L.Get("TabWarningsPlain"), Warnings.Count);

            InstanceSummary = L.F("InstanceSummaryFmt",
                _settings.ModListName, source, instance.Mods.Count, instance.Downloads.Count);

            // Show what the settings file specifies, flagging profiles missing on disk.
            string Describe(string? name) =>
                string.IsNullOrWhiteSpace(name) ? L.Get("ProfileNoneSet")
                : instance.Profiles.ContainsKey(name) ? name
                : L.F("ProfileNotFoundFmt", name);
            var additional = _settings.AdditionalProfiles.Count > 0
                ? string.Join(", ", _settings.AdditionalProfiles.Select(Describe))
                : L.Get("AdditionalNone");
            ProfilesSummary = L.F("ProfilesSummaryFmt", Describe(_settings.Profile), additional);

            HasAnalysis = true;
            var total = staleCount + RedundantItems.Count + DisabledItems.Count + DownloadItems.Count;
            StatusMessage = total == 0
                ? L.Get("StatusNothingToClean")
                : L.F("StatusAnalysisSummaryFmt", staleCount, RedundantItems.Count,
                    DisabledItems.Count, DownloadItems.Count);
        }
        catch (Exception ex)
        {
            HasAnalysis = false;
            StatusMessage = L.F("StatusAnalysisFailedFmt", ex.Message);
        }
    }

    private static string FormatWarning(AnalysisWarning warning) => warning.Kind switch
    {
        WarningKind.ProfileNotFound => L.F("WarnProfileNotFoundFmt", warning.Subject),
        WarningKind.NoProfilesFound => L.Get("WarnNoProfiles"),
        WarningKind.DownloadsFolderMissing => L.F("WarnDownloadsFolderMissingFmt", warning.Subject),
        WarningKind.DownloadsWithoutMeta => L.F("WarnDownloadsWithoutMetaFmt", warning.Count),
        WarningKind.UnknownArchiveMetas => L.F("WarnUnknownArchiveMetasFmt", warning.Count),
        _ => warning.Kind.ToString(),
    };

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
            StatusMessage = L.Get("StatusNoChanges");
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
                ? L.F("StatusSavedFmt", changes.Count, Path.GetFileName(_loadedSettingsPath))
                : L.F("StatusSavedBackupFmt", changes.Count, Path.GetFileName(backupPath));
        }
        catch (Exception ex)
        {
            StatusMessage = L.F("StatusSaveFailedFmt", ex.Message);
        }
    }
}
