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
public partial class MissingDownloadItem : ObservableObject
{
    public static readonly string[] Actions = ["Untagged", "Include", "No match include"];

    public MissingDownloadItem(MissingDownloadFinding finding)
    {
        Finding = finding;
        // Both tags on one mod is legal but unusual; treating the original index as
        // "none of the options" makes an apply normalize it to the single selected tag.
        OriginalActionIndex = finding switch
        {
            { TaggedInclude: true, TaggedNoMatchInclude: true } => -1,
            { TaggedInclude: true } => 1,
            { TaggedNoMatchInclude: true } => 2,
            _ => 0,
        };
        _selectedActionIndex = finding.TaggedInclude ? 1 : finding.TaggedNoMatchInclude ? 2 : 0;
    }

    public MissingDownloadFinding Finding { get; }

    public string ModName => Finding.ModName;
    public string Reason => Finding.Reason;

    public string CurrentStatus => "Currently: " + Finding switch
    {
        { TaggedInclude: true, TaggedNoMatchInclude: true } => "Include + No match include",
        { TaggedInclude: true } => "Include",
        { TaggedNoMatchInclude: true } => "No match include",
        _ => "untagged",
    };

    public int OriginalActionIndex { get; }

    public bool IsChanged => SelectedActionIndex != OriginalActionIndex;

    /// <summary>Index into <see cref="Actions"/>.</summary>
    [ObservableProperty]
    private int _selectedActionIndex;
}
