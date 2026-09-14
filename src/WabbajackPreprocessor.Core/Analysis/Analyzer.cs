using WabbajackPreprocessor.Core.Mo2;
using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.Core.Analysis;

/// <summary>
/// Cross-references a compiler settings file with the MO2 instance it describes and
/// produces the three finding lists the preprocessor acts on, plus general warnings.
/// </summary>
public static class Analyzer
{
    public static AnalysisResult Analyze(CompilerSettings settings, Mo2Instance instance)
    {
        var warnings = new List<AnalysisWarning>();

        // Selected profiles: the main profile plus AdditionalProfiles, kept when present on disk.
        var profilesUsed = new List<string>();
        foreach (var profile in settings.AllProfiles)
        {
            if (instance.Profiles.ContainsKey(profile))
                profilesUsed.Add(profile);
            else
                warnings.Add(new AnalysisWarning(WarningKind.ProfileNotFound, profile));
        }
        if (profilesUsed.Count == 0)
            warnings.Add(new AnalysisWarning(WarningKind.NoProfilesFound));

        // A mod counts as enabled if any selected profile's modlist enables it (Wabbajack
        // rules: '+' lines and separators). disabledIn records the profiles that list a
        // mod as disabled; a mod in neither structure is unlisted everywhere.
        var enabledMods = new HashSet<string>(RelPaths.Comparer);
        var disabledIn = new Dictionary<string, List<string>>(RelPaths.Comparer);
        foreach (var profile in profilesUsed)
        {
            foreach (var entry in instance.Profiles[profile])
            {
                if (entry.Kind == ModlistEntryKind.Foreign)
                    continue;
                if (entry.WabbajackEnabled)
                    enabledMods.Add(entry.Name);
                else
                    (disabledIn.TryGetValue(entry.Name, out var list)
                        ? list
                        : disabledIn[entry.Name] = []).Add(profile);
            }
        }

        var (staleEntries, redundantAlwaysEnabled) =
            FindStaleEntries(settings, instance, enabledMods, disabledIn);
        var disabledMods = FindDisabledMods(settings, instance, enabledMods, disabledIn);
        var modsWithoutDownload = FindModsWithoutDownload(settings, instance, enabledMods, warnings);

        var metalessCount = instance.Downloads.Count(d => !d.HasMeta);
        if (metalessCount > 0)
            warnings.Add(new AnalysisWarning(WarningKind.DownloadsWithoutMeta, Count: metalessCount));
        var unknownCount = instance.Downloads.Count(d => d.UnknownArchive);
        if (unknownCount > 0)
            warnings.Add(new AnalysisWarning(WarningKind.UnknownArchiveMetas, Count: unknownCount));

        return new AnalysisResult
        {
            ProfilesUsed = profilesUsed,
            StaleEntries = staleEntries,
            RedundantAlwaysEnabled = redundantAlwaysEnabled,
            DisabledMods = disabledMods,
            ModsWithoutDownload = modsWithoutDownload,
            Warnings = warnings,
        };
    }

    private static (List<StaleEntry> Stale, List<StaleEntry> Redundant) FindStaleEntries(
        CompilerSettings settings, Mo2Instance instance, HashSet<string> enabledMods,
        Dictionary<string, List<string>> disabledIn)
    {
        var stale = new List<StaleEntry>();
        var redundant = new List<StaleEntry>();
        foreach (var list in Enum.GetValues<TagList>())
        {
            var entries = settings.GetTagList(list);
            var seen = new HashSet<string>(RelPaths.Comparer);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var normalized = RelPaths.Normalize(entry);

                if (!seen.Add(normalized))
                {
                    stale.Add(new StaleEntry(list, i, entry, StaleEntryKind.Duplicate));
                    continue;
                }

                var fullPath = Path.Combine(instance.SourcePath, normalized);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    stale.Add(new StaleEntry(list, i, entry, StaleEntryKind.Missing));
                    continue;
                }

                if (list == TagList.AlwaysEnabled)
                {
                    // The tag is inert only when the mod is enabled in every selected
                    // profile that lists it. If even one selected profile disables the
                    // mod, the tag preserves that profile's '-' line in the shipped
                    // modlist.txt (IncludeThisProfile.ReadAndCleanModlist), so it is
                    // doing real work and must not be reported.
                    var parts = RelPaths.Split(normalized);
                    if (parts.Length >= 2
                        && RelPaths.Comparer.Equals(parts[0], "mods")
                        && enabledMods.Contains(parts[1])
                        && !disabledIn.ContainsKey(parts[1]))
                    {
                        redundant.Add(new StaleEntry(list, i, entry, StaleEntryKind.RedundantAlwaysEnabled));
                    }
                }
            }
        }
        return (stale, redundant);
    }

    private static List<DisabledModFinding> FindDisabledMods(
        CompilerSettings settings, Mo2Instance instance,
        HashSet<string> enabledMods, Dictionary<string, List<string>> disabledIn)
    {
        var findings = new List<DisabledModFinding>();
        foreach (var mod in instance.Mods)
        {
            if (mod.IsSeparator || enabledMods.Contains(mod.Name))
                continue;
            if (RelPaths.IsCoveredBy(mod.RelativePath, settings.AlwaysEnabled))
                continue; // already handled
            if (RelPaths.IsCoveredBy(mod.RelativePath, settings.Ignore))
                continue; // deliberately excluded

            findings.Add(new DisabledModFinding(mod.Name,
                disabledIn.GetValueOrDefault(mod.Name) ?? []));
        }
        return findings;
    }

    private static List<MissingDownloadFinding> FindModsWithoutDownload(
        CompilerSettings settings, Mo2Instance instance,
        HashSet<string> enabledMods, List<AnalysisWarning> warnings)
    {
        if (!Directory.Exists(instance.DownloadsPath))
            warnings.Add(new AnalysisWarning(WarningKind.DownloadsFolderMissing, instance.DownloadsPath));

        var downloadsByName = new HashSet<string>(
            instance.Downloads.Select(d => d.FileName), RelPaths.Comparer);
        var downloadsByIds = new HashSet<(string, string)>(
            instance.Downloads
                .Where(d => !string.IsNullOrEmpty(d.ModId) && !string.IsNullOrEmpty(d.FileId))
                .Select(d => (d.ModId!.Trim(), d.FileId!.Trim())));
        var downloadModIds = new HashSet<string>(
            instance.Downloads
                .Select(d => d.ModId?.Trim() ?? "")
                .Where(id => id.Length > 0 && id != "0" && id != "-1"),
            StringComparer.Ordinal);

        // Link cascade (see docs/wabbajack-formats.md §4).
        bool HasTraceableDownload(Mo2.ModFolder mod)
        {
            var installationFile = mod.InstallationFile?.Trim();
            if (!string.IsNullOrEmpty(installationFile))
            {
                var found = Path.IsPathRooted(installationFile)
                    ? File.Exists(installationFile)
                    : downloadsByName.Contains(Path.GetFileName(installationFile))
                      || File.Exists(Path.Combine(instance.DownloadsPath, installationFile));
                if (found)
                    return true;
            }

            if (mod.InstalledFiles.Any(f => downloadsByIds.Contains((f.ModId.Trim(), f.FileId.Trim()))))
                return true;

            var nexusId = mod.NexusModId?.Trim();
            return !string.IsNullOrEmpty(nexusId) && nexusId != "0" && nexusId != "-1"
                   && downloadModIds.Contains(nexusId);
        }

        var findings = new List<MissingDownloadFinding>();
        var downloadedMods = new List<Mo2.ModFolder>();
        foreach (var mod in instance.Mods)
        {
            if (mod.IsSeparator)
                continue;

            if (HasTraceableDownload(mod))
            {
                // Its folder stands in for its archive's contents in the overlap scan
                // below — enabled or not, since Wabbajack indexes every download.
                downloadedMods.Add(mod);
                continue;
            }

            // Only mods that will actually be compiled matter: enabled in a selected
            // profile, or force-kept via AlwaysEnabled.
            var compiled = enabledMods.Contains(mod.Name)
                           || RelPaths.IsCoveredBy(mod.RelativePath, settings.AlwaysEnabled);
            if (!compiled)
                continue;

            // Ignored mods never reach the compiled output; nothing to decide.
            if (RelPaths.IsCoveredBy(mod.RelativePath, settings.Ignore))
                continue;

            // Exact mods\<name> entries are the mod's current, editable status.
            // A mod covered only by a broader entry (e.g. a whole parent folder tagged)
            // can't be untagged individually, so it is still skipped.
            var taggedInclude = settings.Include.Any(e => RelPaths.AreEqual(e, mod.RelativePath));
            var taggedNoMatchInclude = settings.NoMatchInclude.Any(e => RelPaths.AreEqual(e, mod.RelativePath));
            if ((!taggedInclude && RelPaths.IsCoveredBy(mod.RelativePath, settings.Include))
                || (!taggedNoMatchInclude && RelPaths.IsCoveredBy(mod.RelativePath, settings.NoMatchInclude)))
                continue;

            var installationFile = mod.InstallationFile?.Trim();
            var reason = !mod.HasMetaIni
                ? MissingDownloadReason.NoMetaIni
                : !string.IsNullOrEmpty(installationFile)
                    ? MissingDownloadReason.ArchiveNotFound
                    : MissingDownloadReason.NoInstallationFile;
            findings.Add(new MissingDownloadFinding(mod.Name, reason, taggedInclude, taggedNoMatchInclude)
            {
                InstallationFile = reason == MissingDownloadReason.ArchiveNotFound ? installationFile : null,
            });
        }

        return AddFileOverlap(instance, findings, downloadedMods);
    }

    /// <summary>
    /// Cheap patch-mod detection: a file in a no-download mod that also exists (by
    /// Data-relative path, or failing that by bare file name — approximating
    /// Wabbajack's name-based IncludePatches lookup) inside some downloaded mod's
    /// folder will typically be stored as a binary diff against that archive, needing
    /// no tag. Loose files only; counterparts inside BSAs are not seen, so overlap is
    /// under-reported (the safe direction). The root meta.ini is ignored on both sides.
    /// </summary>
    private static List<MissingDownloadFinding> AddFileOverlap(
        Mo2Instance instance, List<MissingDownloadFinding> findings,
        List<Mo2.ModFolder> downloadedMods)
    {
        if (findings.Count == 0)
            return findings;

        var knownPaths = new HashSet<string>(RelPaths.Comparer);
        var knownNames = new HashSet<string>(RelPaths.Comparer);
        foreach (var mod in downloadedMods)
        {
            foreach (var (relPath, name) in EnumerateModFiles(instance, mod.Name))
            {
                knownPaths.Add(relPath);
                knownNames.Add(name);
            }
        }

        for (var i = 0; i < findings.Count; i++)
        {
            int total = 0, overlap = 0, autoInlined = 0;
            var uniqueSample = new List<string>();
            foreach (var (relPath, name) in EnumerateModFiles(instance, findings[i].ModName))
            {
                total++;
                if (AutoInlinedExtensions.Contains(Path.GetExtension(name)))
                    autoInlined++;
                else if (knownPaths.Contains(relPath) || knownNames.Contains(name))
                    overlap++;
                else if (uniqueSample.Count < 5)
                    uniqueSample.Add(relPath);
            }
            findings[i] = findings[i] with
            {
                TotalFileCount = total,
                OverlapFileCount = overlap,
                AutoInlinedFileCount = autoInlined,
                UniqueFileSample = uniqueSample,
            };
        }
        return findings;
    }

    /// <summary>
    /// Extensions Wabbajack inlines with no tag needed: Consts.ConfigFileExtensions
    /// (IncludeAllConfigs) plus .txt (IncludeRegex(".*\.txt")). Note .toml is NOT in
    /// the set. See docs/wabbajack-formats.md §2.1 steps 10 and 12.
    /// </summary>
    private static readonly HashSet<string> AutoInlinedExtensions = new(
        [".json", ".ini", ".yml", ".xml", ".yaml", ".compiler_settings", ".mo2_compiler_settings", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<(string RelPath, string Name)> EnumerateModFiles(
        Mo2Instance instance, string modName)
    {
        var modDir = Path.Combine(instance.SourcePath, "mods", modName);
        if (!Directory.Exists(modDir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(modDir, "*", SearchOption.AllDirectories))
        {
            var relPath = RelPaths.Normalize(Path.GetRelativePath(modDir, file));
            if (RelPaths.Comparer.Equals(relPath, "meta.ini"))
                continue; // MO2 metadata, not archive content
            yield return (relPath, Path.GetFileName(file));
        }
    }
}
