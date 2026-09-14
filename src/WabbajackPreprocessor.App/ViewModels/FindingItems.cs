using CommunityToolkit.Mvvm.ComponentModel;
using WabbajackPreprocessor.Core.Analysis;

namespace WabbajackPreprocessor.App.ViewModels;

/// <summary>Row for the "stale entries" tab: a tag-list entry that can be removed.</summary>
public partial class StaleEntryItem : ObservableObject
{
    public StaleEntryItem(StaleEntry finding)
    {
        Finding = finding;
        // Redundant AlwaysEnabled entries are harmless, so leave them unchecked by default.
        _remove = finding.Kind != StaleEntryKind.RedundantAlwaysEnabled;
    }

    public StaleEntry Finding { get; }

    public string Entry => Finding.Entry;
    public string Detail => $"{Finding.List} — {Finding.Reason}";

    [ObservableProperty]
    private bool _remove;
}

/// <summary>Row for the "disabled mods" tab.</summary>
public partial class DisabledModItem(DisabledModFinding finding) : ObservableObject
{
    public string ModName => finding.ModName;
    public string Note => finding.Note;

    [ObservableProperty]
    private bool _markAlwaysEnabled;
}

/// <summary>Row for the "no matching download" tab.</summary>
public partial class MissingDownloadItem(MissingDownloadFinding finding) : ObservableObject
{
    public static readonly string[] Actions = ["Leave as is", "Include", "No match include"];

    public string ModName => finding.ModName;
    public string Reason => finding.Reason;

    /// <summary>Index into <see cref="Actions"/>.</summary>
    [ObservableProperty]
    private int _selectedActionIndex;
}
