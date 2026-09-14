using CommunityToolkit.Mvvm.ComponentModel;
using WabbajackPreprocessor.App.Localization;
using WabbajackPreprocessor.Core;
using WabbajackPreprocessor.Core.Analysis;
using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.App.ViewModels;

/// <summary>Row for the "stale entries" and "redundant Always Enabled" tabs.</summary>
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

    /// <summary>Shown only for findings whose reason varies per row (redundant
    /// AlwaysEnabled names the mod); generic reasons live in the sub-group header.</summary>
    public string Detail => Finding.Kind == StaleEntryKind.RedundantAlwaysEnabled
        ? L.F("RedundantReasonFmt", RelPaths.Split(Finding.Entry).ElementAtOrDefault(1) ?? Finding.Entry)
        : "";

    public bool HasDetail => Detail.Length > 0;

    public string RemoveAccessibleName => Finding.Kind == StaleEntryKind.RedundantAlwaysEnabled
        ? L.F("RemoveAETagAccessibleFmt", Finding.Entry)
        : L.F("RemoveEntryAccessibleFmt", Finding.Entry);

    [ObservableProperty]
    private bool _remove;
}

/// <summary>A kind-based sub-list within one tag list's section.</summary>
public sealed class StaleSubGroup(StaleEntryKind kind, IReadOnlyList<StaleEntryItem> items)
{
    public IReadOnlyList<StaleEntryItem> Items { get; } = items;

    public string Header { get; } = L.F("TabCountFmt", kind switch
    {
        StaleEntryKind.Missing => L.Get("SubMissing"),
        StaleEntryKind.Duplicate => L.Get("SubDuplicate"),
        _ => kind.ToString(),
    }, items.Count);
}

/// <summary>One tag list's section on the "stale entries" tab.</summary>
public sealed class StaleGroup
{
    public StaleGroup(TagList list, IReadOnlyList<StaleEntryItem> items)
    {
        Items = items;
        Header = L.F("TabCountFmt", list switch
        {
            TagList.NoMatchInclude => L.Get("ListNoMatchInclude"),
            TagList.Include => L.Get("ListInclude"),
            TagList.Ignore => L.Get("ListIgnore"),
            TagList.AlwaysEnabled => L.Get("ListAlwaysEnabled"),
            _ => list.ToString(),
        }, items.Count);
        SubGroups = items
            .GroupBy(i => i.Finding.Kind)
            .OrderBy(g => g.Key)
            .Select(g => new StaleSubGroup(g.Key, [.. g]))
            .ToList();
    }

    /// <summary>All of the section's items, flattened (used when applying).</summary>
    public IReadOnlyList<StaleEntryItem> Items { get; }

    public IReadOnlyList<StaleSubGroup> SubGroups { get; }

    public string Header { get; }
}

/// <summary>Row for the "disabled mods" tab.</summary>
public partial class DisabledModItem(DisabledModFinding finding) : ObservableObject
{
    public string ModName => finding.ModName;

    public string Note => finding.DisabledInProfiles.Count > 0
        ? L.F("DisabledInFmt", string.Join(", ", finding.DisabledInProfiles))
        : L.Get("NotListedNote");

    public string MarkAccessibleName => L.F("MarkAEAccessibleFmt", finding.ModName);

    [ObservableProperty]
    private bool _markAlwaysEnabled;
}

/// <summary>Row for the "no matching download" tab.</summary>
public partial class MissingDownloadItem : ObservableObject
{
    public static readonly string[] Actions =
        [L.Get("ActionUntagged"), L.Get("ActionInclude"), L.Get("ActionNoMatchInclude")];

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

    public string Reason => Finding.Reason switch
    {
        MissingDownloadReason.NoMetaIni => L.Get("ReasonNoMetaIni"),
        MissingDownloadReason.ArchiveNotFound => L.F("ReasonArchiveNotFoundFmt", Finding.InstallationFile),
        _ => L.Get("ReasonNoInstallationFile"),
    };

    public string CurrentStatus => L.F("CurrentlyFmt", Finding switch
    {
        { TaggedInclude: true, TaggedNoMatchInclude: true } => L.Get("TagStatusBoth"),
        { TaggedInclude: true } => L.Get("TagStatusInclude"),
        { TaggedNoMatchInclude: true } => L.Get("TagStatusNoMatchInclude"),
        _ => L.Get("TagStatusUntagged"),
    });

    public string TagActionAccessibleName => L.F("TagActionAccessibleFmt", Finding.ModName);

    /// <summary>Patch-mod flag: files that also exist in downloaded mods are typically
    /// stored as binary diffs, and config/text files are inlined automatically — neither
    /// needs a tag. Only the remaining unique files do.</summary>
    public string OverlapSummary
    {
        get
        {
            if (Finding.TotalFileCount == 0)
                return L.Get("OverlapNoFiles");
            if (Finding.HandledFileCount == 0)
                return "";

            var how = new List<string>();
            if (Finding.OverlapFileCount > 0)
                how.Add(L.F("OverlapDiffPartFmt", Finding.OverlapFileCount));
            if (Finding.AutoInlinedFileCount > 0)
                how.Add(L.F("OverlapConfigPartFmt", Finding.AutoInlinedFileCount));

            var unique = Finding.TotalFileCount - Finding.HandledFileCount;
            return unique == 0
                ? L.F("OverlapAllFmt", Finding.TotalFileCount, string.Join("; ", how))
                : L.F("OverlapPartialFmt", Finding.HandledFileCount, Finding.TotalFileCount,
                    string.Join("; ", how), unique, string.Join(", ", Finding.UniqueFileSample));
        }
    }

    public bool HasOverlapSummary => OverlapSummary.Length > 0;

    public int OriginalActionIndex { get; }

    public bool IsChanged => SelectedActionIndex != OriginalActionIndex;

    /// <summary>Index into <see cref="Actions"/>.</summary>
    [ObservableProperty]
    private int _selectedActionIndex;
}
