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
/// <c>mods\&lt;name&gt;</c> entry in the corresponding list.</summary>
public sealed record MissingDownloadFinding(
    string ModName, string Reason, bool TaggedInclude, bool TaggedNoMatchInclude);

public sealed class AnalysisResult
{
    public required IReadOnlyList<string> ProfilesUsed { get; init; }
    public required IReadOnlyList<StaleEntry> StaleEntries { get; init; }
    public required IReadOnlyList<DisabledModFinding> DisabledMods { get; init; }
    public required IReadOnlyList<MissingDownloadFinding> ModsWithoutDownload { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}
