using CommunityToolkit.Mvvm.ComponentModel;
using WabbajackPreprocessor.Core.Analysis;
using WabbajackPreprocessor.Core.Settings;

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
    public string Detail => Finding.Reason; // the tag list is shown by the group header

    [ObservableProperty]
    private bool _remove;
}

/// <summary>One tag list's section on the "stale entries" tab.</summary>
public sealed class StaleGroup(TagList list, IReadOnlyList<StaleEntryItem> items)
{
    public IReadOnlyList<StaleEntryItem> Items { get; } = items;

    public string Header { get; } = list switch
    {
        TagList.NoMatchInclude => "No match include",
        TagList.Include => "Include",
        TagList.Ignore => "Ignore",
        TagList.AlwaysEnabled => "Always enabled",
        _ => list.ToString(),
    } + $" ({items.Count})";
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

    /// <summary>Patch-mod flag: files that also exist in downloaded mods are typically
    /// stored as binary diffs, and config/text files are inlined automatically — neither
    /// needs a tag. Only the remaining unique files do.</summary>
    public string OverlapSummary
    {
        get
        {
            if (Finding.TotalFileCount == 0)
                return "Mod folder contains no files.";
            if (Finding.HandledFileCount == 0)
                return "";

            var how = new List<string>();
            if (Finding.OverlapFileCount > 0)
                how.Add($"{Finding.OverlapFileCount} also exist in downloaded mods (stored as binary diffs)");
            if (Finding.AutoInlinedFileCount > 0)
                how.Add($"{Finding.AutoInlinedFileCount} are config/text files Wabbajack always inlines");

            var unique = Finding.TotalFileCount - Finding.HandledFileCount;
            return unique == 0
                ? $"⚑ All {Finding.TotalFileCount} file(s) are handled without a tag: {string.Join("; ", how)}. " +
                  "A tag isn't strictly needed (No match include is safe insurance; avoid Include)."
                : $"⚑ {Finding.HandledFileCount} of {Finding.TotalFileCount} files are handled without a tag " +
                  $"({string.Join("; ", how)}) — {unique} unique file(s) still need inlining " +
                  $"(e.g. {string.Join(", ", Finding.UniqueFileSample)}); No match include recommended.";
        }
    }

    public bool HasOverlapSummary => OverlapSummary.Length > 0;

    public int OriginalActionIndex { get; }

    public bool IsChanged => SelectedActionIndex != OriginalActionIndex;

    /// <summary>Index into <see cref="Actions"/>.</summary>
    [ObservableProperty]
    private int _selectedActionIndex;
}
