using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.Core.Analysis;

public enum StaleEntryKind
{
    /// <summary>The entry points at a file/folder that no longer exists under Source.</summary>
    Missing,
    /// <summary>The entry duplicates an earlier entry in the same list.</summary>
    Duplicate,
    /// <summary>AlwaysEnabled entry for a mod that is already enabled in a selected profile.</summary>
    RedundantAlwaysEnabled,
}

/// <summary>A tag-list entry the user may want to remove. <paramref name="Index"/> is the
/// entry's position in its list at analysis time (used for precise removal).</summary>
public sealed record StaleEntry(TagList List, int Index, string Entry, StaleEntryKind Kind, string Reason);

/// <summary>A mod that is disabled (per Wabbajack's rules) in every selected profile and
/// not yet marked AlwaysEnabled.</summary>
public sealed record DisabledModFinding(string ModName, string Note);

/// <summary>A mod that will be compiled but has no traceable download archive.
/// The Tagged flags report whether the settings file already carries an exact
/// <c>mods\&lt;name&gt;</c> entry in the corresponding list. The file-overlap fields
/// support spotting patch mods: a file that also exists in a downloaded mod's folder
/// is likely stored by Wabbajack as a binary diff against that mod's archive
/// (IncludePatches runs before NoMatchInclude), so it needs no tag; only files with
/// no counterpart anywhere must be inlined.</summary>
public sealed record MissingDownloadFinding(
    string ModName, string Reason, bool TaggedInclude, bool TaggedNoMatchInclude,
    int TotalFileCount = 0, int OverlapFileCount = 0, int AutoInlinedFileCount = 0)
{
    /// <summary>Up to five example files that are neither auto-inlined nor have a
    /// counterpart in any downloaded mod — the ones a tag actually exists for.</summary>
    public IReadOnlyList<string> UniqueFileSample { get; init; } = [];

    /// <summary>Files Wabbajack handles without any tag: diff-patched or auto-inlined.</summary>
    public int HandledFileCount => OverlapFileCount + AutoInlinedFileCount;
}

public sealed class AnalysisResult
{
    public required IReadOnlyList<string> ProfilesUsed { get; init; }

    /// <summary>Dead references only: missing paths and duplicates.</summary>
    public required IReadOnlyList<StaleEntry> StaleEntries { get; init; }

    /// <summary>AlwaysEnabled entries whose mod is enabled in every selected profile
    /// that lists it — the tag currently has no effect at all. Kept separate from
    /// <see cref="StaleEntries"/> because removing one changes a live setting rather
    /// than deleting a dead reference. A mod disabled in even one selected profile is
    /// NOT reported: there the tag preserves that profile's disabled modlist.txt line.</summary>
    public required IReadOnlyList<StaleEntry> RedundantAlwaysEnabled { get; init; }
    public required IReadOnlyList<DisabledModFinding> DisabledMods { get; init; }
    public required IReadOnlyList<MissingDownloadFinding> ModsWithoutDownload { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}
