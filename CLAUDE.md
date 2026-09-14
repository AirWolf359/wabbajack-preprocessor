# CLAUDE.md

Desktop cleanup tool for Wabbajack modlist authors: loads a `*.compiler_settings`
file, cross-references it with the Mod Organizer 2 instance it describes, and
lets the user fix stale tag entries, disabled mods, and mods without downloads.
Distributed via GitHub Releases (and eventually Nexus) as a self-contained
Windows exe.

## Commands

- Build: `dotnet build`
- Test: `dotnet test` (xUnit, all tests in `tests/WabbajackPreprocessor.Core.Tests`)
- Run GUI: `dotnet run --project src/WabbajackPreprocessor.App` (optionally pass a
  `.compiler_settings` path as the first argument to auto-analyze)
- Release build (what CI does on `v*` tags): `dotnet publish src/WabbajackPreprocessor.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`

## Architecture

- `src/WabbajackPreprocessor.Core` — all logic, deliberately UI-free (a CLI front
  end may be added later):
  - `Settings/` — `CompilerSettings` DTO mirroring Wabbajack's class property-for-property,
    `CompilerSettingsFile` load/save (timestamped `.bak` before save)
  - `Mo2/` — `IniFile` (tolerant Qt QSettings-style INI reader), `ModlistFile`
    (`modlist.txt`), `Mo2Instance` (scans `mods/`, `profiles/`, downloads)
  - `Analysis/` — `Analyzer` (produces the three finding lists + warnings),
    `SettingsUpdater` (applies a `SettingsUpdatePlan`)
  - `RelPaths` — Wabbajack-style relative path helpers
- `src/WabbajackPreprocessor.App` — Avalonia MVVM GUI (CommunityToolkit.Mvvm).
  `MainViewModel` orchestrates; row types live in `ViewModels/FindingItems.cs`.

`docs/wabbajack-formats.md` is the authoritative reference for every file format
(researched from Wabbajack 4.2.x and MO2 sources). Consult it before changing any
parser or serialization behavior.

## Hard requirements

- **Round-trip fidelity**: re-serializing an unmodified settings file must
  reproduce it byte-for-byte (PascalCase property names in declaration order,
  nulls written, unknown properties preserved via `ExtraProperties`, `\uXXXX`
  escaping, 2-space indent). `SampleFileTests` enforces this against real files
  in `samples/`.
- **Path semantics**: relative paths use backslashes, compare case-insensitively
  and segment-wise (`mods\Foo` covers `mods\Foo\x` but not `mods\FooBar`).
- **Wabbajack rules**: separators (`*_separator`) count as enabled;
  `Ignore` beats the other tag lists; only mods enabled in a selected profile or
  listed in `AlwaysEnabled` are compiled.
- Target **.NET 9** (installed SDK). Avalonia templates and CommunityToolkit
  C# 14 partial-property syntax (`public partial string X { get; set; }`) don't
  compile here — use classic `[ObservableProperty] private string _x;`.

## Conventions

- `samples/` holds the user's real modlist data for local validation and is
  git-ignored along with `*.compiler_settings` and `*.wabbajack` — never commit
  any of it, and never publish content from it (paths, mod names) without asking.
- Source control: never commit directly to `main`. Branch (`feature/…`, `fix/…`,
  `ci/…`, `docs/…`) → PR → wait for green CI → squash merge, delete branch.
  Releases are cut by tagging `vX.Y.Z`.
