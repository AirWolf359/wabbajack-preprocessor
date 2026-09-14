using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.Core.Analysis;

// The core library reports findings as structured data — kinds plus parameters —
// never user-facing prose. The UI layer localizes and formats them.

public enum StaleEntryKind
{
    /// <summary>The entry points at a file/folder that no longer exists under Source.</summary>
    Missing,
    /// <summary>The entry duplicates an earlier entry in the same list.</summary>
    Duplicate,
    /// <summary>AlwaysEnabled entry for a mod that is enabled in every selected profile.</summary>
    RedundantAlwaysEnabled,
}

/// <summary>A tag-list entry the user may want to remove. <paramref name="Index"/> is the
/// entry's position in its list at analysis time (used for precise removal).</summary>
public sealed record StaleEntry(TagList List, int Index, string Entry, StaleEntryKind Kind);

/// <summary>A mod that is disabled (per Wabbajack's rules) in every selected profile and
/// not yet marked AlwaysEnabled. An empty <see cref="DisabledInProfiles"/> means the mod
/// is not listed in any selected profile's modlist.txt at all.</summary>
public sealed record DisabledModFinding(string ModName, IReadOnlyList<string> DisabledInProfiles);

public enum MissingDownloadReason
{
    /// <summary>The mod folder has no meta.ini (hand-created or generated).</summary>
    NoMetaIni,
    /// <summary>meta.ini names an installation archive that is not in the downloads folder.</summary>
    ArchiveNotFound,
    /// <summary>meta.ini exists but records no installation archive.</summary>
    NoInstallationFile,
}

/// <summary>A mod that will be compiled but has no traceable download archive.
/// The Tagged flags report whether the settings file already carries an exact
/// <c>mods\&lt;name&gt;</c> entry in the corresponding list. The file-overlap fields
/// support spotting patch mods: a file that also exists in a downloaded mod's folder
/// is likely stored by Wabbajack as a binary diff against that mod's archive
/// (IncludePatches runs before NoMatchInclude), so it needs no tag; only files with
/// no counterpart anywhere must be inlined.</summary>
public sealed record MissingDownloadFinding(
    string ModName, MissingDownloadReason Reason, bool TaggedInclude, bool TaggedNoMatchInclude,
    int TotalFileCount = 0, int OverlapFileCount = 0, int AutoInlinedFileCount = 0)
{
    /// <summary>meta.ini's installationFile value when <see cref="Reason"/> is
    /// <see cref="MissingDownloadReason.ArchiveNotFound"/>.</summary>
    public string? InstallationFile { get; init; }

    /// <summary>Up to five example files that are neither auto-inlined nor have a
    /// counterpart in any downloaded mod — the ones a tag actually exists for.</summary>
    public IReadOnlyList<string> UniqueFileSample { get; init; } = [];

    /// <summary>Files Wabbajack handles without any tag: diff-patched or auto-inlined.</summary>
    public int HandledFileCount => OverlapFileCount + AutoInlinedFileCount;
}

public enum WarningKind
{
    /// <summary>A profile named in the settings has no modlist.txt on disk (Subject = profile name).</summary>
    ProfileNotFound,
    /// <summary>None of the selected profiles were found on disk.</summary>
    NoProfilesFound,
    /// <summary>The downloads folder does not exist (Subject = path).</summary>
    DownloadsFolderMissing,
    /// <summary>Count downloads have no .meta sidecar.</summary>
    DownloadsWithoutMeta,
    /// <summary>Count download .meta files are marked unknownArchive=true.</summary>
    UnknownArchiveMetas,
}

public sealed record AnalysisWarning(WarningKind Kind, string? Subject = null, int Count = 0);

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
    public required IReadOnlyList<AnalysisWarning> Warnings { get; init; }
}
