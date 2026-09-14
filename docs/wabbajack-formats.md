# Wabbajack File Format Reference

Reference for building a preprocessor for Wabbajack compiler settings files and the
Mod Organizer 2 (MO2) instances they describe.

Researched 2026-09-14 against the `main` branch of
[wabbajack-tools/wabbajack](https://github.com/wabbajack-tools/wabbajack) (current 4.2.x line)
and [ModOrganizer2/modorganizer](https://github.com/ModOrganizer2/modorganizer) (`master`).
All source citations are listed in [Sources](#sources) at the bottom.

---

## 1. The compiler settings file (`*.compiler_settings`)

### 1.1 What it is and where it lives

Wabbajack 3.x/4.x stores all compiler configuration for a modlist in a single JSON file with
the extension **`.compiler_settings`** (defined as `Ext.CompilerSettings = new(".compiler_settings")`
in `Wabbajack.Common/Ext.cs`).

- **Location**: in the **root of the MO2 instance folder** (the compiler `Source`), named after
  the modlist: `<Source>\<ModListName>.compiler_settings`.
  From `CompilerSettingsVM.CompilerSettingsPath` (Wabbajack.App.Wpf):
  `Source.Combine(ModListName).WithExtension(Ext.CompilerSettings)`.
- **Registry of known settings files**: the GUI keeps a list of every settings file it has
  saved in `%LOCALAPPDATA%\Wabbajack\saved_settings\compiler_settings_paths.json`
  (key `Consts.AllSavedCompilerSettingsPaths = "compiler_settings_paths"`, written by
  `SettingsManager`, which stores each key as `<key>.json` under
  `KnownFolders.WabbajackAppLocal.Combine("saved_settings")`; `WabbajackAppLocal` =
  `%LOCALAPPDATA%\Wabbajack`). The file is a JSON array of absolute path strings,
  most-recently-saved first.
- The file is (re)written by `BaseCompilerVM.SaveSettings()` on essentially every edit in the
  compiler UI (including every click in the file-manager tree), serialized **indented**
  (`WriteIndented = true`).
- The CLI (`wabbajack-cli compile -i <path>`) accepts either a `.compiler_settings` file
  directly or an MO2 root folder (in which case settings are inferred; see §2.4).

### 1.2 Serialization rules

Serialization is `System.Text.Json` via `Wabbajack.DTOs.JsonConverters.DTOSerializer`:

- **No naming policy** is set → property names are serialized **exactly as the C# names
  (PascalCase)**.
- Reader options: `NumberHandling = AllowReadingFromString`, `ReadCommentHandling = Skip`,
  `AllowTrailingCommas = true`. A robust parser should accept numbers-as-strings, `//`
  comments and trailing commas, though Wabbajack never writes them.
- No `DefaultIgnoreCondition` → **every property is written**, including nulls
  (e.g. `"Description": null` appears in real files).
- `AbsolutePath` → JSON string, e.g. `"C:\\Modding\\MyList"` (`AbsolutePathConverter`
  writes `value.ToString()`; parts joined with `\`). Parsing accepts `/` or `\` separators.
- `RelativePath` → JSON string with **backslash separators**, e.g. `"mods\\My Mod"`
  (`RelativePathConverter`; `RelativePath.ToString()` = `string.Join('\\', Parts)`).
  Parsing splits on both `/` and `\`, removes empty entries, and **rejects any path whose
  first segment contains `:`** (i.e. absolute paths are invalid `RelativePath`s).
  Comparisons are **case-insensitive** (`InvariantCultureIgnoreCase`).
- `Game` → **string enum member name** (the enum carries
  `[JsonConverter(typeof(JsonStringEnumConverter))]`), e.g. `"SkyrimSpecialEdition"`,
  `"Morrowind"`, `"Fallout4"`, `"SkyrimVR"`, `"Starfield"`, `"OblivionRemastered"` — see
  `Wabbajack.DTOs/Game/Game.cs` for the full list (enum member names, not the
  `[Description]` display names).
- `Version` → string via `VersionConverter` (`System.Version.ToString()`/`Parse`), e.g.
  `"4.0.2"`, `"0.0.1.0"`.
- `TimeSpan` → .NET's built-in constant format, e.g. `"00:01:00"` (confirmed in real files).
- Escaping: Wabbajack's serializer uses System.Text.Json's default escaper, so
  non-ASCII/special chars appear as `\uXXXX` (real files contain e.g. `"Ring\u0027s Patch Repo"`
  for an apostrophe). Parsers must handle standard JSON unicode escapes.

### 1.3 The `CompilerSettings` class — every property

Source: `Wabbajack.Compiler/CompilerSettings.cs` (namespace `Wabbajack.Compiler`).
(NOTE: it lives in the **Wabbajack.Compiler** project, not Wabbajack.DTOs;
`Wabbajack.DTOs/SavedSettings/` only contains `InstallationSettings.cs`.)

| JSON property | C# type | Default | Meaning |
|---|---|---|---|
| `ModlistIsNSFW` | `bool` | `false` | Modlist flagged as adult content. |
| `Source` | `AbsolutePath` | — | **The MO2 instance root folder** (contains `ModOrganizer.ini`, `mods\`, `profiles\`…). All `RelativePath` lists below are relative to this. |
| `Downloads` | `AbsolutePath` | — | The downloads folder (archives + `.meta` files). Usually `<Source>\downloads`, but can be anywhere (read from `ModOrganizer.ini` `[Settings] download_directory` when inferred). |
| `Game` | `Game` (string enum) | — | Primary game, e.g. `"SkyrimSpecialEdition"`. |
| `OutputFile` | `AbsolutePath` | — | Full path of the `.wabbajack` file to produce. (The CLI overrides its directory if `-o` is a directory.) |
| `ModListImage` | `AbsolutePath` | — | Path to the modlist banner image (packed as `modlist-image.png`). |
| `UseGamePaths` | `bool` | `false` | Allow matching source files against files in the installed game folder(s) (game folders are added as VFS roots; needed for Stock Game-less lists / Game Folder Files). |
| `UseTextureRecompression` | `bool` | `false` | Enables the `MatchSimilarTextures` compilation step (fuzzy-match recompressed DDS textures). |
| `OtherGames` | `Game[]` (string enum array) | `[]` | Additional games whose install folders are indexed as file sources (e.g. Fallout 4 for a Fallout 4 VR list). |
| `MaxVerificationTime` | `TimeSpan` | `"00:01:00"` | Max time to spend verifying a download during meta inference/validation. |
| `ModListName` | `string` | `""` | Modlist title. Also determines the settings file name itself. |
| `ModListAuthor` | `string` | `""` | Author name. |
| `ModListDescription` | `string` | `""` | Description (UI limits ~700 chars). |
| `ModListReadme` | `string` | `""` | Readme URL. |
| `ModListWebsite` | `string` | `""` | Website URL. (Was `Uri?` in early 3.x — a `null` here is possible in old files.) |
| `ModListCommunity` | `string` | `""` | Community/Discord URL. (Added in 4.x; absent in 3.0.x files.) |
| `ModlistVersion` | `Version` (string) | `"0.0.1.0"` | **Obsolete** (`[Obsolete("Use Version instead")]`) but still serialized. Use `Version`. |
| `PublishUpdate` | `bool` | `false` | Auto-publish after compiling (requires author CDN access / MachineUrl). |
| `MachineUrl` | `string` | `""` | Machine identifier for publishing, e.g. `"author/ListName"`. |
| `AutoGenerateReport` | `bool` | `false` | Generate a report after compile (added in 4.x). |
| `Profile` | `string` | `""` | **The main MO2 profile name** (folder name under `profiles\`). |
| `AdditionalProfiles` | `string[]` | `[]` | Secondary MO2 profile names also packed into the modlist. (The wiki UI calls this "Other Profiles".) |
| `NoMatchInclude` | `RelativePath[]` | `[]` | Files/folders (relative to `Source`) that are **inlined into the `.wabbajack` file if they don't match any other compilation step** (i.e. can't be traced to a download archive). |
| `Include` | `RelativePath[]` | `[]` | Files/folders (relative to `Source`) **unconditionally inlined** into the `.wabbajack` file. |
| `Ignore` | `RelativePath[]` | `[]` | Files/folders (relative to `Source`) **excluded** from compilation entirely. |
| `AlwaysEnabled` | `RelativePath[]` | `[]` | Mod folders (relative to `Source`, i.e. `mods\<Mod Name>`) whose files are kept **even when the mod is disabled (`-`) in every selected profile's `modlist.txt`**. Also kept as `-` lines in the shipped `modlist.txt`. |
| `Version` | `Version` (string) | `null` | The modlist version, e.g. `"4.0.2"`. |
| `Description` | `string` | `null` | Version-specific changelog/description (shown in the version list; distinct from `ModListDescription`). Frequently `null`. |

Not serialized (`[JsonIgnore]`, computed): `AllProfiles` (= `AdditionalProfiles` + `Profile`),
`IsMO2Modlist` (true if any profile name is non-empty).

Notes for a validator:

- Entries in the four tag lists may denote **files or directories**; Wabbajack never stores a
  trailing separator, and matching is prefix-by-path-segment (`RelativePath.InFolder`) —
  an entry `mods\Foo` matches `mods\Foo` itself and everything under it, but **not**
  `mods\FooBar` (segment-wise comparison, not string-prefix).
- Duplicate entries are legal (the VM round-trips through a `HashSet<RelativePath>`, so
  the GUI dedupes case-insensitively on save, but hand-edited files may contain dupes).
- `AlwaysEnabled` entries are expected to be of the form `mods\<ModName>`
  (`IncludeThisProfile.ReadAndCleanModlist` only honors entries under `mods\` with
  `Level > 1`, and takes `Parts[1]` as the mod name).
- Path comparisons everywhere are case-insensitive; keep that in your preprocessor.

### 1.4 Example (abridged from a real file)

```json
{
  "ModlistIsNSFW": false,
  "Source": "P:\\Modding\\Modlists\\outlander",
  "Downloads": "P:\\Modding\\Modlists\\outlander\\downloads",
  "Game": "Morrowind",
  "OutputFile": "P:\\Modding\\Modlists\\Outlander 4.0.1.wabbajack",
  "ModListImage": "P:\\Modding\\Modlists\\outlander\\Stock Game Folder\\Banner.png",
  "UseGamePaths": false,
  "UseTextureRecompression": true,
  "OtherGames": [],
  "MaxVerificationTime": "00:01:00",
  "ModListName": "Outlander",
  "ModListAuthor": "codering",
  "ModListDescription": "…",
  "ModListReadme": "https://github.com/thomasblasquez/outlander",
  "ModListWebsite": "https://github.com/thomasblasquez/outlander",
  "ModListCommunity": "",
  "ModlistVersion": "0.0.1.0",
  "PublishUpdate": false,
  "MachineUrl": "codering/Outlander",
  "Profile": "Outlander",
  "AdditionalProfiles": [],
  "NoMatchInclude": [
    "Stock Game Folder\\Data Files\\shaders",
    "mods\\MMC Patches",
    "mods\\Ring\u0027s Patch Repo"
  ],
  "Include": [
    "Stock Game Folder\\Data Files\\MWSE",
    "mods\\Merged Objects",
    "Outlander Keybinds.png"
  ],
  "Ignore": [
    ".git",
    "crashDumps",
    "logs",
    "overwrite",
    "Stock Game Folder\\Saves"
  ],
  "AlwaysEnabled": [
    "mods\\Merged Grass",
    "mods\\FGM Grass Plugins"
  ],
  "Version": "4.0.2",
  "Description": null
}
```

---

## 2. Tagging semantics during compilation

### 2.1 How the compiler consumes the lists

The MO2 compiler (`Wabbajack.Compiler/MO2Compiler.cs`, `MakeStack()`) runs every source file
under `Source` through an **ordered stack of steps; the first step that returns a directive
wins**. Relevant ordering (abridged, in order):

1. `IgnoreGameFilesIfGameFolderFilesExist`, `IgnoreSaveFiles`
2. **`IgnoreTaggedFiles(Settings.Ignore)`** — any file whose relative path is inside an
   `Ignore` entry → `IgnoredDirectly` (excluded; runs before nearly everything, so `Ignore`
   beats `Include`/`NoMatchInclude` on overlap).
3. Built-in ignores: `logs\`, `downloads\`, `webcache\`, `overwrite\`, `crashDumps\`,
   paths containing `temporary_logs`, `GPUCache`, `SSEEdit Cache`.
4. `IgnoreOtherProfiles` — anything under `profiles\` not belonging to
   `Profile`/`AdditionalProfiles` → ignored.
5. `IgnoreDisabledMods` — a file under `mods\` is ignored unless its mod is
   (a) `+`-enabled (or a `_separator`) in **any** selected profile's `modlist.txt`, or
   (b) listed in **`AlwaysEnabled`**.
6. `IncludeThisProfile` — every file under the selected profiles' folders is **inlined**;
   `modlist.txt` is rewritten on the fly to keep only `+` lines, `_separator` lines, and
   the `AlwaysEnabled` mods' lines (so disabled-but-always-enabled mods ship disabled).
7. `IncludeStubbedConfigFiles`, various built-in ignores/includes (`*.bat` in root,
   `IncludeModIniData` — every `mods\*\meta.ini` is inlined, etc.)
8. `DirectMatch` — **the core step**: match the file by hash against the indexed download
   archives (and game folders if `UseGamePaths`); produces `FromArchive` directives.
9. **`IncludeTaggedFiles(Settings.Include)`** — files inside `Include` entries → inlined
   into the `.wabbajack` (`InlineFile`). Note this runs *after* `DirectMatch`, so a file
   that hash-matches an archive is stored as a cheap archive reference; `Include` is the
   fallback for those that don't.
10. Patch building (`DeconstructBSAs`, `MatchSimilarTextures` if enabled, `IncludePatches`,
    `IncludeDummyESPs`), misc built-in ignores/includes (`.html`, `.bin`, `.pyc`, `.log`
    ignored; `categories.dat`, `nexuscatmap.dat`, `splash.png` included; all config-file
    extensions via `IncludeAllConfigs`).
11. **`IncludeTaggedFiles(Settings.NoMatchInclude)`** — placed near the *end* of the stack:
    anything inside a `NoMatchInclude` entry that survived this far (i.e. matched nothing)
    is inlined. This is what "include if no match" means mechanically — same step type as
    `Include`, just a lower priority position.
12. `IncludeRegex(".*\\.txt")`, a few final ignores, then `DropAll` — any remaining file
    becomes a `NoMatch` error; **one or more `NoMatch` results abort the compile**
    (`ACompiler.CheckForNoMatchExit`).

Semantics summary:

| List | Effect | Typical use |
|---|---|---|
| `Ignore` | File never enters the modlist. Highest priority of the four. | `.git`, logs, caches, personal files, `WABBAJACK_ALWAYS_DISABLE` mods. |
| `Include` | File bytes are embedded ("inlined") in the `.wabbajack` even if it might have matched later steps' heuristics. | Tooling, merged plugins, keybind images — never downloaded-mod content (copyright + bloat). |
| `NoMatchInclude` | File bytes are embedded **only if nothing else matched** (esp. no hash match to a download). Prevents "unmatched file" compile failures. | Generated content: merges, grass cache, patch outputs, shader dumps. |
| `AlwaysEnabled` | Mod folder's files are compiled even though the mod is `-` (disabled) in the profile; the shipped `modlist.txt` keeps its (disabled) line. | Optional content the user can enable post-install. |

### 2.2 How the UI edits these lists

`Wabbajack.App.Wpf/ViewModels/Compiler/CompilerFileManagerVM.cs` + `FileTreeItemVM.cs`:
the "File Manager" tab shows the `Source` tree; each file/folder can be given any
combination of four flag states (a `[Flags]` enum `CompilerFileState`:
`NoMatchInclude=1, Include=2, Ignore=4, AlwaysEnabled=8`). Checking/unchecking a state
adds/removes that item's `PathRelativeToRoot` in the corresponding
`HashSet<RelativePath>` on `CompilerSettingsVM` and immediately calls `SaveSettings()`
(rewrites the `.compiler_settings` file). Multiple flags on one path are allowed by the UI.
Folders show "contains" markers when a descendant (but not the folder itself) is tagged.

### 2.3 Legacy in-instance tags (still supported)

Before the settings file grew these lists (WJ 2.x), authors tagged content inside the MO2
instance itself; the inferencer still reads all of these (constants in
`Wabbajack.Compiler/Consts.cs`):

- **Tag files** (extension-less empty files named exactly, placed in a folder — the *parent
  folder* of the tag file gets tagged... note: in current inferencer code, a tag file in the
  **instance root** tags `file.Parent`, i.e. the root-level scan enumerates `Source` files):
  `WABBAJACK_INCLUDE`, `WABBAJACK_NOMATCH_INCLUDE`, `WABBAJACK_IGNORE`,
  `WABBAJACK_INCLUDE_SAVES`.
- **List files**: `WABBAJACK_NOMATCH_INCLUDE_FILES.txt` / `WABBAJACK_IGNORE_FILES.txt`
  (also `.TXT`) — each line is a filename (relative to the tag file's folder) to
  no-match-include/ignore; for the NOMATCH variant, lines naming *directories* go to
  `NoMatchInclude` and lines naming *files* go to `Include`.
- **meta.ini flags**: the strings `WABBAJACK_INCLUDE`, `WABBAJACK_NOMATCH_INCLUDE`,
  `WABBAJACK_IGNORE`, `WABBAJACK_ALWAYS_ENABLE`, `WABBAJACK_ALWAYS_DISABLE` appearing
  anywhere in a mod's `meta.ini` `[General]` `notes=` or `comments=` values tag that
  `mods\<Mod>` folder into the corresponding list (`ALWAYS_ENABLE`→`AlwaysEnabled`,
  `ALWAYS_DISABLE`→`Ignore`).

### 2.4 Settings inference

`Wabbajack.Compiler/CompilerSettingsInferencer.cs` builds a `CompilerSettings` from a
`profiles\<profile>\modlist.txt` path (used when the author starts from scratch, and by the
CLI when given an MO2 root):

- `Source` = `modlist.txt`'s `../../..`; requires `<Source>\ModOrganizer.ini`.
- `Game` = fuzzy match of `[General] gameName`; `Profile`/`ModListName` =
  `[General] selected_profile`; `Downloads` = `[Settings] download_directory`
  (fallback `<Source>\downloads`); `OutputFile` = `<Source parent>\<name>.wabbajack`.
- Values are decoded with `FromMO2Ini()` — strips Qt `@ByteArray(...)` wrappers and
  unescapes `\\`-style INI escapes.
- Tag files, `*_FILES.txt` lists, mod `meta.ini` notes/comments flags (see §2.3), and
  `profiles\<profile>\otherprofiles.txt` (one profile name per line → `AdditionalProfiles`)
  are all folded into the settings.

---

## 3. Mod Organizer 2 instance layout (the compiler `Source`)

A portable MO2 instance root contains:

```
<Source>\
├── ModOrganizer.ini            # instance config (Qt QSettings INI)
├── portable.txt                # marks a portable instance (WJ ignores this file)
├── mods\
│   └── <Mod Name>\             # one folder per mod, name = mod name in modlist.txt
│       ├── meta.ini            # per-mod metadata (may be missing for hand-made folders)
│       └── ...mod files (Data-relative layout)
├── profiles\
│   └── <Profile Name>\
│       ├── modlist.txt         # mod enable/disable + order
│       ├── plugins.txt         # plugin (esp/esm/esl) enable + order
│       ├── loadorder.txt       # (older) full plugin order
│       ├── archives.txt
│       ├── settings.ini        # profile-local settings (QSettings INI)
│       ├── lockedorder.txt
│       └── [game INIs if profile-local INIs on]  # skyrim.ini, skyrimprefs.ini, …
├── downloads\                  # default; actual location from ModOrganizer.ini
│   ├── SomeMod-1234-1-0-123456789.7z
│   ├── SomeMod-1234-1-0-123456789.7z.meta
│   └── ...
├── overwrite\                  # generated files (WJ always ignores)
├── webcache\ , logs\ , crashDumps\   # WJ always ignores
└── <modlist name>.compiler_settings  # Wabbajack's settings file (§1)
```

### 3.1 `ModOrganizer.ini` (keys Wabbajack reads)

Qt `QSettings` INI. Relevant keys:

```ini
[General]
gameName=Skyrim Special Edition
selected_profile=@ByteArray(MyProfile)
gamePath=@ByteArray(C:\\Steam\\steamapps\\common\\Skyrim Special Edition)

[Settings]
download_directory=D:/Modding/downloads
```

Quirks a parser must handle (Qt QSettings serialization):

- Values may be wrapped in `@ByteArray(...)`.
- Backslashes inside values are escaped (`\\`); forward slashes also appear.
- `%20`-style percent-encoding can appear in **keys**.
- Wabbajack parses with ini-parser configured `AllowDuplicateKeys` and
  `AllowDuplicateSections` — duplicates exist in the wild; last-wins is fine.

### 3.2 `profiles\<profile>\modlist.txt` — exact format

Written by `Profile::doWriteModlist()` in MO2 (`src/profile.cpp`):

- Encoding UTF-8, line terminator `\r\n`.
- First line is always the comment
  `# This file was automatically generated by Mod Organizer.`
  (any line starting `#` should be treated as a comment).
- Then one line per mod, **in reverse priority order**: the FIRST mod line is the
  HIGHEST priority (wins file conflicts, loads last); the last line is priority 0.
- Line = single prefix char + mod name (the exact `mods\` subfolder name, may contain
  spaces and any filesystem-legal chars; no quoting, no escaping):
  - `+Name` — mod enabled
  - `-Name` — mod disabled
  - `*Name` — "foreign"/unmanaged mod (game DLC, Creation Club, etc.; has no `mods\` folder)
  - Separators are ordinary entries whose name ends with `_separator`
    (backing folder `mods\<Name>_separator` exists, containing only a `meta.ini`).
    MO2 writes them with `-`; **Wabbajack treats any line ending in `_separator` as
    enabled** (`line.StartsWith("+") || line.EndsWith("_separator")`) and keeps them.
- Mods with "automatic priority" (backup mods, overwrite) are not written.

Wabbajack's reading (in `IgnoreDisabledMods` / `IncludeThisProfile`): enabled set =
lines starting `+` plus lines ending `_separator`; mod name = `line[1..]` (trimmed).
It does not treat `*` lines as mods folders (they have none).

### 3.3 `mods\<Mod>\meta.ini` — per-mod metadata

Qt QSettings INI written by `ModInfoRegular::saveMeta()` (MO2 `src/modinforegular.cpp`).
Top-level keys land in the `[General]` section. Full key set MO2 writes:

```ini
[General]
gameName=SkyrimSE                     ; MO2 game short-name (NOT the same as WJ Game enum)
modid=1234                            ; Nexus mod ID (-1/0 if none)
version=1.0
newestVersion=1.1
ignoredVersion=
category=42,                          ; "<primary>,<other,...>" MO2 category IDs
installationFile=SomeMod-1234-1-0-123456789.7z   ; ← the mod→download link (§4)
repository=Nexus
comments=
notes=WABBAJACK_NOMATCH_INCLUDE       ; free text; WJ scans notes/comments for WABBAJACK_* flags
nexusDescription=...
url=
hasCustomURL=false
nexusFileStatus=1
lastNexusQuery=2024-01-01T00:00:00Z   ; ISO dates
lastNexusUpdate=...
nexusLastModified=...
nexusCategory=0
author=...
uploader=...
uploaderUrl=...
converted=false
validated=false
color=@Variant(...)                   ; Qt-serialized color; treat as opaque
endorsed=0                            ; only present when known
tracked=0                             ; only present when known

[installedFiles]                      ; QSettings array: which Nexus files were merged in
size=1
1\modid=1234
1\fileid=56789

[Plugins]                             ; optional plugin-specific settings groups
...
```

Notes:

- Any subset may be present; hand-created mod folders often have a minimal or missing
  `meta.ini`. Values can be empty.
- Booleans are `true`/`false`; QSettings escaping rules from §3.1 apply.
- Separator folders (`..._separator`) and tool output folders also carry `meta.ini`s.
- Wabbajack inlines every mod's `meta.ini` into the modlist (`IncludeModIniData`) and reads
  only `notes`/`comments` (for flags) during inference.

### 3.4 `downloads\` — archives and `.meta` files

Each downloaded archive `<file>` may have a sidecar **`<file>.meta`** (full filename plus
`.meta`, e.g. `SomeMod-1234-1-0-123456789.7z.meta` — Wabbajack computes it as
`f.WithExtension(Ext.Meta)`, which *appends*). Qt QSettings INI, `[General]` section.

Written by MO2's `DownloadManager::createMetaFile()` (`src/downloadmanager.cpp`):

```ini
[General]
gameName=SkyrimSE          ; MO2 archive short-name for the game
modID=1234
fileID=56789
url=https://...;https://...   ; ';'-joined mirror list
name=Main File 1.0         ; Nexus file name (or "<resolving>"/empty)
description=...
modName=Some Mod
version=1.0
newestVersion=1.0
fileTime=...
fileCategory=1
category=42
repository=Nexus
userData=@Variant(...)
author=...
uploader=...
uploaderUrl=...
installed=true
uninstalled=false
paused=false
removed=false
```

What **Wabbajack** needs from a `.meta` (it ignores the rest): enough for one of its
downloaders' `Resolve(iniData)` to produce a download state, checked in priority order.
Key patterns:

- **Nexus** (`NexusDownloader.Resolve`): requires all of `gameName=`, `modID=`, `fileID=`
  non-empty. `gameName` is matched against MO2 archive short-names
  (`GameRegistry.GetByMO2ArchiveName`) or Nexus names (`SkyrimSE`, `Skyrim`, `Fallout4`,
  `Morrowind`, `NewVegas`, …).
- **Direct HTTP** (`HttpDownloader`): `directURL=<url>` (optionally
  `directURLHeaders=a|b`).
- Other downloaders use downloader-specific keys (GoogleDrive/Mega/MediaFire/ModDB
  manual URLs, `manualURL=`, WabbajackCDN, GameFile, etc.).
- `unknownArchive=true` — written by Wabbajack itself (`ACompiler.InferMetas`) when it
  couldn't infer a source for an archive; such an archive is **unusable** and will fail
  `GatherArchives` if any compiled file needs it.

Compiler behavior around downloads (`MO2Compiler.Begin`):

- **Only files in `Downloads` that have a sibling `.meta` are indexed as archives.**
  Wabbajack first attempts to *infer* missing `.meta`s by hash lookup against the
  Wabbajack server (writing them to disk); anything still meta-less is invisible to
  matching.
- Files inside every indexed archive are hashed (nested archives included) into the VFS;
  `DirectMatch` then resolves each source file by hash — **duplicate/old archives
  containing the same file can therefore be matched arbitrarily** (docs recommend deleting
  unused/old downloads).
- At the end (`GatherArchives`/`ResolveArchive`), every archive actually referenced must
  resolve to a valid, allow-listed download state, or compilation fails.

---

## 4. Mapping `mods\<Mod>` → download archive

There is no single authoritative link; use this cascade (what MO2 itself records):

1. **`meta.ini` `[General] installationFile`** — the archive MO2 installed the mod from.
   Usually just the archive **filename** (resolve against the `Downloads` folder);
   can occasionally be an **absolute path** (installed from outside the downloads dir).
   Empty/missing for hand-created mods, separators, and tool-generated folders.
2. **`meta.ini` `[installedFiles]`** (`N\modid` + `N\fileid`) — match against each download
   `.meta`'s `modID`/`fileID` (both Nexus IDs). Handles renamed archives.
3. **`meta.ini` `[General] modid` + `repository`** — weaker: find downloads whose `.meta`
   has the same `modID` (any file of that mod).

Preprocessor checks this enables:

- *Mod with no corresponding download*: enabled mod folder whose `installationFile` is
  empty or points to a non-existent archive AND `[installedFiles]` matches no download
  `.meta` → its files will only survive compilation via hash-match to some *other*
  archive, `Include`/`NoMatchInclude` tags, or game files — otherwise compile fails with
  `NoMatch`.
- *Download with no `.meta`*: will be meta-inferred (network) or dead weight; flag it.
- *`.meta` with `unknownArchive=true`*: unresolvable source; compile fails if referenced.
- *Disabled mod not in `AlwaysEnabled`*: entirely ignored (its downloads may be prunable
  if nothing else references them — but remember matching is hash-based across all
  archives).

Note: Wabbajack itself never uses `installationFile` — it matches purely by hash. The
`installationFile` linkage is for humans/preprocessors; a "mod has no download" warning is
a heuristic, not proof the compile will fail (and vice versa).

---

## 5. Profiles and `AdditionalProfiles`

- An MO2 **profile** is a folder under `profiles\` holding `modlist.txt`, `plugins.txt`,
  optional profile-local game INIs, saves, etc. `ModOrganizer.ini`'s
  `[General] selected_profile` names the active one.
- Wabbajack compiles **one main profile** (`Profile`) plus any `AdditionalProfiles`:
  - Union of their `modlist.txt` enabled sets decides which `mods\` folders are compiled
    (`IgnoreDisabledMods` reads `profiles\<p>\modlist.txt` for every selected profile).
  - All files under each selected profile's folder are inlined (`IncludeThisProfile`),
    with each `modlist.txt` rewritten to drop disabled non-AlwaysEnabled lines.
  - Every other `profiles\*` folder is ignored (`IgnoreOtherProfiles`).
- Legacy authoring aid: a file `profiles\<main>\otherprofiles.txt` (one profile name per
  line) is read by the **inferencer** to seed `AdditionalProfiles`. The settings file is
  authoritative once it exists.
- `CompilerSettingsVM.ProfilePath` = `<Source>\profiles\<Profile>\modlist.txt`.

---

## 6. Version differences (2.x vs 3.x/4.x)

Target the 3.x/4.x format; 2.x is listed for recognition only.

| | Wabbajack 2.x | Wabbajack 3.x / 4.x (current) |
|---|---|---|
| Settings file | No unified file. Per-profile `profiles\<p>\compiler_settings.json` held only `{ "IncludedGames": [], "OtherProfiles": [] }` (`Wabbajack.Lib/CompilerSettings.cs` @ 2.4.4.4). GUI state (name, author, image, …) lived in Wabbajack's own saved-settings store under `%LOCALAPPDATA%\Wabbajack`. | Single `<Source>\<ModListName>.compiler_settings` JSON (§1) holding everything, including the four tag lists. |
| Tagging | Only WABBAJACK_* tag files / `_FILES.txt` lists / meta.ini notes+comments flags (§2.3). | Tag lists in the settings file, edited via GUI file manager; legacy tags still honored by the inferencer when creating settings from `modlist.txt`. |
| Selecting input | Pick `profiles\<p>\modlist.txt` (or `native_compiler_settings.json` for non-MO2 lists). | Pick `modlist.txt` (infer) or an existing `.compiler_settings`. |
| Extra profiles | `otherprofiles.txt` in profile folder. | `AdditionalProfiles` in settings (inferred from `otherprofiles.txt` if present). |
| Extra games | `IncludedGames` in profile `compiler_settings.json`. | `OtherGames` in settings. |
| Registry of settings | `last-saved-compiler-settings` key (early 3.0 WPF) | `%LOCALAPPDATA%\Wabbajack\saved_settings\compiler_settings_paths.json` (list of absolute paths). |

3.0.x → 4.x property drift within the same file format (all additive):

- `ModListWebsite` was `Uri?` in 3.0.x (could be `null`), now `string`.
- `ModListCommunity`, `AutoGenerateReport`, `PublishUpdate` added along the way; absent
  keys deserialize to defaults, unknown keys are ignored — a parser should do the same.
- `.mo2_compiler_settings` appears in `Consts.ConfigFileExtensions` as a historical/legacy
  config extension; the live format is `.compiler_settings`.
- The obsolete `ModlistVersion` is still written; treat `Version` as authoritative.

---

## 7. Parser implementation checklist

- JSON: PascalCase keys; accept unknown keys, missing keys (use defaults), `null` strings,
  `\uXXXX` escapes, comments/trailing commas (WJ's reader does).
- Paths: normalize `/`↔`\`; compare case-insensitively, segment-wise; `RelativePath` never
  starts with a drive letter; serialize back with `\`.
- Game: string enum member names from `Wabbajack.DTOs/Game/Game.cs`.
- `TimeSpan`: `hh:mm:ss` (constant "c" format, may include days/fractions).
- INI (all of `ModOrganizer.ini`, `meta.ini`, `*.meta`, `settings.ini`): tolerate duplicate
  keys/sections, `@ByteArray(...)`, `@Variant(...)`, escaped backslashes, missing sections;
  keys outside any section belong to `[General]` conceptually (QSettings writes an explicit
  `[General]` header).
- `modlist.txt`: skip `#` comments/empty lines; prefix `+`/`-`/`*`; `_separator` suffix ⇒
  separator (treated as enabled by WJ); reverse priority order; UTF-8; CRLF.
- Download `.meta` discovery: sidecar file is `<archive filename>.meta` (appended, not
  replaced extension).

---

## Sources

Wabbajack (`wabbajack-tools/wabbajack@main`, via raw.githubusercontent.com, 2026-09-14):

- `Wabbajack.Compiler/CompilerSettings.cs` — the settings class (§1.3)
- `Wabbajack.Compiler/CompilerSettingsInferencer.cs` — inference, tag files, otherprofiles.txt
- `Wabbajack.Compiler/Consts.cs` — WABBAJACK_* constants, folder names
- `Wabbajack.Compiler/MO2Compiler.cs` — compilation stack, downloads indexing
- `Wabbajack.Compiler/ACompiler.cs` — meta inference, archive resolution, NoMatch exit
- `Wabbajack.Compiler/CompilationSteps/{IgnoreDisabledMods,IgnoreOtherProfiles,IncludeThisProfile,IncludeTaggedFiles,IgnoreTaggedFiles}.cs`
- `Wabbajack.DTOs/JsonConverters/{DTOSerializer,DIExtensions,AbsolutePathConverter,RelativePathConverter,VersionConverter}.cs`
- `Wabbajack.DTOs/Game/Game.cs` — Game enum + `JsonStringEnumConverter`
- `Wabbajack.Paths/RelativePath.cs`, `Wabbajack.Paths/AbsolutePath.cs` — path semantics
- `Wabbajack.App.Wpf/ViewModels/Compiler/{CompilerSettingsVM,BaseCompilerVM,CompilerHomeVM,CompilerFileManagerVM,FileTreeItemVM}.cs` — save location, UI editing
- `Wabbajack.App.Wpf/Consts.cs`, `Wabbajack.Services.OSIntegrated/{SettingsManager,ServiceExtensions}.cs`, `Wabbajack.Paths.IO/KnownFolders.cs` — saved_settings location
- `Wabbajack.CLI/Verbs/Compile.cs` — CLI settings loading
- `Wabbajack.Downloaders.Nexus/NexusDownloader.cs`, `Wabbajack.Downloaders.Http/HttpDownloader.cs`, `Wabbajack.Downloaders.Dispatcher/DownloadDispatcher.cs` — `.meta` keys
- `Wabbajack.Installer/IniExtensions.cs` — INI parsing / `FromMO2Ini`
- Historical: tag `2.4.4.4` (`Wabbajack.Lib/CompilerSettings.cs`, `Wabbajack.Lib/MO2Compiler.cs`, `Wabbajack/View Models/Compilers/MO2CompilerVM.cs`), tag `3.0.1.9` (`Wabbajack.Compiler/CompilerSettings.cs`, `Wabbajack.App.Wpf/View Models/Compilers/CompilerVM.cs`)

Mod Organizer 2 (`ModOrganizer2/modorganizer@master`):

- `src/modinforegular.cpp` (`ModInfoRegular::saveMeta`) — meta.ini keys
- `src/downloadmanager.cpp` (`DownloadManager::createMetaFile`) — download `.meta` keys
- `src/profile.cpp` (`Profile::doWriteModlist`) — modlist.txt format

Docs:

- [Wabbajack wiki — Compilation Settings](https://wiki.wabbajack.org/modlist_author_documentation/Compilation%20Settings.html)
- [Wabbajack wiki — Pre-Compilation](https://wiki.wabbajack.org/modlist_author_documentation/Pre-Compilation.html)

Real-world sample: `thomasblasquez/outlander` — `Outlander.compiler_settings` (GitHub).
