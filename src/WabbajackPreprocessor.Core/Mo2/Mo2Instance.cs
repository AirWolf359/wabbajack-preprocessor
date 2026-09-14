namespace WabbajackPreprocessor.Core.Mo2;

/// <summary>A mod folder under <c>mods\</c>, with the meta.ini fields the preprocessor uses.</summary>
public sealed class ModFolder
{
    public required string Name { get; init; }

    /// <summary>Path relative to the instance root, e.g. <c>mods\My Mod</c>.</summary>
    public string RelativePath => $"mods\\{Name}";

    public bool IsSeparator => Name.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);

    public bool HasMetaIni { get; init; }

    /// <summary>meta.ini [General] installationFile — archive the mod was installed from.</summary>
    public string? InstallationFile { get; init; }

    /// <summary>meta.ini [General] modid (Nexus mod ID as written; "-1"/"0"/empty mean none).</summary>
    public string? NexusModId { get; init; }

    /// <summary>meta.ini [installedFiles] (modid, fileid) pairs.</summary>
    public IReadOnlyList<(string ModId, string FileId)> InstalledFiles { get; init; } = [];
}

/// <summary>A file in the downloads folder (excluding .meta sidecars themselves).</summary>
public sealed class DownloadArchive
{
    public required string FileName { get; init; }
    public bool HasMeta { get; init; }
    public string? ModId { get; init; }
    public string? FileId { get; init; }
    /// <summary>.meta written by Wabbajack when it couldn't infer the archive's source.</summary>
    public bool UnknownArchive { get; init; }
}

/// <summary>
/// A scanned MO2 instance: mod folders, profiles (with parsed modlist.txt), and downloads.
/// </summary>
public sealed class Mo2Instance
{
    public required string SourcePath { get; init; }
    public required string DownloadsPath { get; init; }
    public required IReadOnlyList<ModFolder> Mods { get; init; }
    public required IReadOnlyDictionary<string, List<ModlistEntry>> Profiles { get; init; }
    public required IReadOnlyList<DownloadArchive> Downloads { get; init; }

    public ModFolder? FindMod(string name) =>
        Mods.FirstOrDefault(m => RelPaths.Comparer.Equals(m.Name, name));

    /// <summary>
    /// Loads an instance. <paramref name="downloadsPath"/> falls back to
    /// ModOrganizer.ini's [Settings] download_directory, then <c>&lt;source&gt;\downloads</c>.
    /// Missing folders yield empty collections rather than errors.
    /// </summary>
    public static Mo2Instance Load(string sourcePath, string? downloadsPath = null)
    {
        if (string.IsNullOrWhiteSpace(downloadsPath))
        {
            var moIni = IniFile.TryLoad(Path.Combine(sourcePath, "ModOrganizer.ini"));
            downloadsPath = moIni?.Get("Settings", "download_directory");
            if (string.IsNullOrWhiteSpace(downloadsPath))
                downloadsPath = Path.Combine(sourcePath, "downloads");
            else if (!Path.IsPathRooted(downloadsPath))
                downloadsPath = Path.Combine(sourcePath, downloadsPath);
        }

        return new Mo2Instance
        {
            SourcePath = sourcePath,
            DownloadsPath = downloadsPath,
            Mods = LoadMods(Path.Combine(sourcePath, "mods")),
            Profiles = LoadProfiles(Path.Combine(sourcePath, "profiles")),
            Downloads = LoadDownloads(downloadsPath),
        };
    }

    private static List<ModFolder> LoadMods(string modsPath)
    {
        var mods = new List<ModFolder>();
        if (!Directory.Exists(modsPath))
            return mods;

        foreach (var dir in Directory.EnumerateDirectories(modsPath))
        {
            var meta = IniFile.TryLoad(Path.Combine(dir, "meta.ini"));
            var installedFiles = new List<(string, string)>();
            var installedSection = meta?.GetSection("installedFiles");
            if (installedSection is not null)
            {
                foreach (var (key, modId) in installedSection)
                {
                    // QSettings array entries: "1\modid" paired with "1\fileid"
                    if (!key.EndsWith("\\modid", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var index = key[..^"\\modid".Length];
                    var fileId = installedSection.GetValueOrDefault($"{index}\\fileid");
                    if (!string.IsNullOrEmpty(fileId))
                        installedFiles.Add((modId, fileId));
                }
            }

            mods.Add(new ModFolder
            {
                Name = Path.GetFileName(dir),
                HasMetaIni = meta is not null,
                InstallationFile = meta?.Get(IniFile.DefaultSection, "installationFile"),
                NexusModId = meta?.Get(IniFile.DefaultSection, "modid"),
                InstalledFiles = installedFiles,
            });
        }
        return mods;
    }

    private static Dictionary<string, List<ModlistEntry>> LoadProfiles(string profilesPath)
    {
        var profiles = new Dictionary<string, List<ModlistEntry>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(profilesPath))
            return profiles;

        foreach (var dir in Directory.EnumerateDirectories(profilesPath))
        {
            var modlist = Path.Combine(dir, "modlist.txt");
            if (File.Exists(modlist))
                profiles[Path.GetFileName(dir)] = ModlistFile.Load(modlist);
        }
        return profiles;
    }

    private static List<DownloadArchive> LoadDownloads(string downloadsPath)
    {
        var downloads = new List<DownloadArchive>();
        if (!Directory.Exists(downloadsPath))
            return downloads;

        foreach (var file in Directory.EnumerateFiles(downloadsPath))
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            // The sidecar is "<full file name>.meta" (extension appended, not replaced).
            var meta = IniFile.TryLoad(file + ".meta");
            downloads.Add(new DownloadArchive
            {
                FileName = Path.GetFileName(file),
                HasMeta = meta is not null,
                ModId = meta?.Get(IniFile.DefaultSection, "modID"),
                FileId = meta?.Get(IniFile.DefaultSection, "fileID"),
                UnknownArchive = string.Equals(
                    meta?.Get(IniFile.DefaultSection, "unknownArchive"), "true",
                    StringComparison.OrdinalIgnoreCase),
            });
        }
        return downloads;
    }
}
