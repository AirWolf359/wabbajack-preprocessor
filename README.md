# Wabbajack Preprocessor

![CI](https://github.com/AirWolf359/wabbajack-preprocessor/actions/workflows/ci.yml/badge.svg)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A cleanup tool for [Wabbajack](https://www.wabbajack.org/) modlist authors.

As a modlist evolves over many revisions, its compiler settings file
(`*.compiler_settings`) gets cluttered: tag lists keep referencing mods that were
removed long ago, disabled mods silently drop out of the compiled list, and
generated content fails compilation because it can't be traced back to a download.
Wabbajack never cleans any of this up itself. This tool does.

Point it at your `.compiler_settings` file and it cross-references the settings
with your Mod Organizer 2 instance — showing which profile is the default and
which additional profiles are included — then walks you through interactive
cleanup tabs, each holding exactly one kind of decision:

- **Stale entries** — dead references in the `NoMatchInclude` / `Include` /
  `Ignore` / `AlwaysEnabled` lists: entries pointing at files or folders that no
  longer exist, plus duplicates, grouped by list. Removing them never changes
  what gets compiled.
- **Redundant Always Enabled** — mods whose `AlwaysEnabled` tag currently does
  nothing because the mod is enabled in every selected profile that lists it.
  (Multi-profile aware: a mod disabled in even one selected profile keeps its
  tag unflagged, since the tag preserves that profile's disabled line.)
- **Disabled mods** — mods disabled in every selected profile, which Wabbajack
  would silently skip. Choose which ones to mark **Always Enabled** so they ship
  (still disabled) for users to opt into.
- **No download** — mods that will be compiled but can't be traced to any
  archive in your downloads folder. Each row shows the mod's current tag and
  lets you set it to **Include**, **No match include**, or untagged. Likely
  patch mods are flagged: files that also exist in downloaded mods are stored
  by Wabbajack as binary diffs, and config/text files are inlined
  automatically, so the row tells you whether a tag is actually needed and
  which files drive that.

A warnings tab also flags missing profiles, downloads missing their `.meta`
sidecar, and `.meta` files marked `unknownArchive=true`.

## A supplement to Wabbajack, not a replacement

This tool deliberately does **only** pre-compilation cleanup. It does not
compile modlists, does not wrap or invoke the Wabbajack CLI, and never will —
that is a deliberate scope decision, not a missing feature. The Wabbajack team
builds and maintains the hard parts (compilation, downloaders, hosting,
validation), and this project exists to support that work, not to absorb its
interface. Run this tool to tidy your compiler settings, then compile in
Wabbajack itself as usual. Contributions that turn this into a compilation
front end will be declined.

Applying your choices rewrites the settings file **after saving a timestamped
backup next to it**. The writer reproduces Wabbajack's own JSON formatting
byte-for-byte, so untouched parts of the file never change.

## Usage

1. Grab the latest zip from
   [Releases](https://github.com/AirWolf359/wabbajack-preprocessor/releases) and
   extract it anywhere (self-contained; no .NET install needed), or build from
   source (below).
2. Launch `WabbajackPreprocessor.App.exe`. Browse to your
   `<modlist>.compiler_settings` file (normally in the root of your MO2
   instance), or pass its path as a command-line argument.
3. Click **Analyze**, review the tabs, make your choices.
4. Click **Apply changes**. A `.bak` copy of the previous settings file is
   written alongside before anything is saved.

> **Note:** "no matching download" is a heuristic. Wabbajack itself matches files
> purely by hash, so a flagged mod may still compile fine if its files happen to
> match another archive. The tool tells you what deserves a look; the decision
> stays yours.

## Accessibility

The app supports Windows assistive technology via UI Automation: it works with
screen readers (Narrator, NVDA, JAWS), every control — including each row's
checkbox and tag dropdown — announces what it acts on, the whole UI is keyboard
navigable with visible focus, and status updates are announced as they happen.
Accessibility is a standing requirement for UI changes here; if you hit
something that doesn't work with your assistive setup, please open an issue.

## Building from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```
dotnet build          # build everything
dotnet test           # run the test suite
dotnet run --project src/WabbajackPreprocessor.App   # launch the GUI
```

## Project layout

| Path | Contents |
|---|---|
| `src/WabbajackPreprocessor.Core` | UI-free logic: settings JSON round-trip, MO2 parsers (`modlist.txt`, Qt-style INI), instance scanner, analyzer, updater |
| `src/WabbajackPreprocessor.App` | Avalonia GUI (MVVM) |
| `tests/WabbajackPreprocessor.Core.Tests` | xUnit tests, including an end-to-end run over a synthetic MO2 instance |
| `docs/wabbajack-formats.md` | Detailed reference for every file format involved, researched from the Wabbajack and MO2 source code |

The core library has no UI dependencies, so a command-line front end can be added
without touching the analysis logic.

## License

[MIT](LICENSE)
