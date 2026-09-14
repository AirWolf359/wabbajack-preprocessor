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
        var warnings = new List<string>();

        // Selected profiles: the main profile plus AdditionalProfiles, kept when present on disk.
        var profilesUsed = new List<string>();
        foreach (var profile in settings.AllProfiles)
        {
            if (instance.Profiles.ContainsKey(profile))
                profilesUsed.Add(profile);
            else
                warnings.Add($"Profile '{profile}' from the settings file has no profiles\\{profile}\\modlist.txt in the instance.");
        }
        if (profilesUsed.Count == 0)
            warnings.Add("None of the selected profiles were found; disabled-mod and download checks are limited.");

        // A mod counts as enabled if any selected profile's modlist enables it (Wabbajack rules:
        // '+' lines and separators). Track listed-anywhere separately to spot unlisted folders.
        var enabledMods = new HashSet<string>(RelPaths.Comparer);
        var listedMods = new HashSet<string>(RelPaths.Comparer);
        var disabledIn = new Dictionary<string, List<string>>(RelPaths.Comparer);
        foreach (var profile in profilesUsed)
        {
            foreach (var entry in instance.Profiles[profile])
            {
                if (entry.Kind == ModlistEntryKind.Foreign)
                    continue;
                listedMods.Add(entry.Name);
                if (entry.WabbajackEnabled)
                    enabledMods.Add(entry.Name);
                else
                    (disabledIn.TryGetValue(entry.Name, out var list)
                        ? list
                        : disabledIn[entry.Name] = []).Add(profile);
            }
        }

        var staleEntries = FindStaleEntries(settings, instance, enabledMods);
        var disabledMods = FindDisabledMods(settings, instance, enabledMods, listedMods, disabledIn);
        var modsWithoutDownload = FindModsWithoutDownload(settings, instance, enabledMods, warnings);

        var metalessCount = instance.Downloads.Count(d => !d.HasMeta);
        if (metalessCount > 0)
            warnings.Add($"{metalessCount} file(s) in the downloads folder have no .meta sidecar; Wabbajack will try to infer their source or ignore them.");
        var unknownCount = instance.Downloads.Count(d => d.UnknownArchive);
        if (unknownCount > 0)
            warnings.Add($"{unknownCount} download .meta file(s) are marked unknownArchive=true; compilation fails if any matched file needs them.");

        return new AnalysisResult
        {
            ProfilesUsed = profilesUsed,
            StaleEntries = staleEntries,
            DisabledMods = disabledMods,
            ModsWithoutDownload = modsWithoutDownload,
            Warnings = warnings,
        };
    }

    private static List<StaleEntry> FindStaleEntries(
        CompilerSettings settings, Mo2Instance instance, HashSet<string> enabledMods)
    {
        var findings = new List<StaleEntry>();
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
                    findings.Add(new StaleEntry(list, i, entry, StaleEntryKind.Duplicate,
                        "Duplicate of an earlier entry in the same list."));
                    continue;
                }

                var fullPath = Path.Combine(instance.SourcePath, normalized);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    findings.Add(new StaleEntry(list, i, entry, StaleEntryKind.Missing,
                        "No such file or folder under the source directory."));
                    continue;
                }

                if (list == TagList.AlwaysEnabled)
                {
                    var parts = RelPaths.Split(normalized);
                    if (parts.Length >= 2
                        && RelPaths.Comparer.Equals(parts[0], "mods")
                        && enabledMods.Contains(parts[1]))
                    {
                        findings.Add(new StaleEntry(list, i, entry, StaleEntryKind.RedundantAlwaysEnabled,
                            $"Mod '{parts[1]}' is already enabled in a selected profile, so Always Enabled has no effect."));
                    }
                }
            }
        }
        return findings;
    }

    private static List<DisabledModFinding> FindDisabledMods(
        CompilerSettings settings, Mo2Instance instance,
        HashSet<string> enabledMods, HashSet<string> listedMods,
        Dictionary<string, List<string>> disabledIn)
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

            var note = listedMods.Contains(mod.Name)
                ? $"Disabled in: {string.Join(", ", disabledIn.GetValueOrDefault(mod.Name) ?? [])}"
                : "Not listed in any selected profile's modlist.txt.";
            findings.Add(new DisabledModFinding(mod.Name, note));
        }
        return findings;
    }

    private static List<MissingDownloadFinding> FindModsWithoutDownload(
        CompilerSettings settings, Mo2Instance instance,
        HashSet<string> enabledMods, List<string> warnings)
    {
        if (!Directory.Exists(instance.DownloadsPath))
            warnings.Add($"Downloads folder '{instance.DownloadsPath}' does not exist.");

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

        var findings = new List<MissingDownloadFinding>();
        foreach (var mod in instance.Mods)
        {
            if (mod.IsSeparator)
                continue;

            // Only mods that will actually be compiled matter: enabled in a selected
            // profile, or force-kept via AlwaysEnabled.
            var compiled = enabledMods.Contains(mod.Name)
                           || RelPaths.IsCoveredBy(mod.RelativePath, settings.AlwaysEnabled);
            if (!compiled)
                continue;

            // Already tagged: nothing for the user to decide.
            if (RelPaths.IsCoveredBy(mod.RelativePath, settings.Include)
                || RelPaths.IsCoveredBy(mod.RelativePath, settings.NoMatchInclude)
                || RelPaths.IsCoveredBy(mod.RelativePath, settings.Ignore))
                continue;

            // Link cascade (see docs/wabbajack-formats.md §4).
            var installationFile = mod.InstallationFile?.Trim();
            if (!string.IsNullOrEmpty(installationFile))
            {
                var found = Path.IsPathRooted(installationFile)
                    ? File.Exists(installationFile)
                    : downloadsByName.Contains(Path.GetFileName(installationFile))
                      || File.Exists(Path.Combine(instance.DownloadsPath, installationFile));
                if (found)
                    continue;
            }

            if (mod.InstalledFiles.Any(f => downloadsByIds.Contains((f.ModId.Trim(), f.FileId.Trim()))))
                continue;

            var nexusId = mod.NexusModId?.Trim();
            if (!string.IsNullOrEmpty(nexusId) && nexusId != "0" && nexusId != "-1"
                && downloadModIds.Contains(nexusId))
                continue;

            var reason = !mod.HasMetaIni
                ? "No meta.ini — likely a hand-created or generated mod folder."
                : !string.IsNullOrEmpty(installationFile)
                    ? $"Installed from '{installationFile}', which is not in the downloads folder."
                    : "meta.ini records no installation archive.";
            findings.Add(new MissingDownloadFinding(mod.Name, reason));
        }
        return findings;
    }
}
