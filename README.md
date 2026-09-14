This project is intended to be a preprocessor for the Wabbajack compiler settings file that is used to compile a mod list for games such as Skyrim or Fallout using the Wabbajack tool. The idea here is that, as you are developing a modlist, the compiler settings file that you create over the course of multiple revisions will become somewhat cluttered. It will have references to mods that are no longer part of the list, but it does not clean up those references itself. There are also additional tasks that you have to do when you are compiling the mod list in Wabbajack itself, such as marking mods as ignore, no match include, or always enabled.
This project is intended to be a tool that will act as a pre-processor for the compiler settings file, and it will do multiple things:
- Detect any mods that are referenced in the compiler settings file that are no longer present in the mod list and clean those up.
- Note any mods that are currently disabled in the mod list and present them in a list so that you can choose whether to mark them as always enabled or not.
- Detect any mods that do not have a corresponding download file in the downloads folder so that you can determine whether or not to mark them as include or no match include.
Things to be determined include:
- what programming language to use for this project
- what other framework or frameworks to use for this project
- how it should be presented and executed
My current vision is a graphical program of some kind, rather than a command-line tool. Alternatively, we might be able to have a command-line interface to the tool that someone could run as part of a workflow. The primary interface will be a graphical interface that will present the information in an easy-to-read format and allow the user to do what the tool is intended to do.
For example, it will:
- present a list of mods that are no longer in the list and allow the user to remove those from the compiler settings file
- present a list of mods that are disabled and allow the user to choose whether or not they should be marked as always enabled
- present a list of mods that don't have a corresponding download and allow the user to choose whether they should be marked as include or no match include
What the AI coding tool will need to do is to:
1. Ask for a sample compiler settings file.
2. Do any necessary research on the format of the file and how Wabbajack itself works.
3. Ask the user for links to documentation, if necessary.
The end result will be a tool that will be published to Nexus and that a mod developer can then download and execute and point at their compiler settings file, as well as the folder in which the mod list resides (including the downloads folder for the various mod archives). It will then check to see what profiles exist and ask the user which profile is the primary profile that they intend to choose when using Wabbajack to compile the list. If necessary, ask for any other profiles that are going to be included. As far as I know, it won't be necessary for this tool to ask for any other profiles because it's the primary profile that determines how these mods need to be tagged.
Note that I dictated the contents of this file using Wispr Flow, and it's possible that it may have misspelled the word Wabbajack somewhere. If you have any questions about whether a particular word is supposed to be the word Wabbajack, ask.

---

## Current status

Decisions made:
- **Language/UI**: C# / .NET 9 with Avalonia (MVVM via CommunityToolkit.Mvvm). GUI-only for v1; the analysis logic is isolated in a core library so a CLI front end can be added later.
- Sample settings files and modlist data placed in the project folder are excluded from source control (see `.gitignore`: `samples/`, `*.compiler_settings`, `*.wabbajack`).

Layout:
- `src/WabbajackPreprocessor.Core` — parsers (compiler settings JSON, MO2 `modlist.txt`, Qt-style INI for `meta.ini`/download `.meta`), instance scanner, the analyzer that produces the three finding lists, and the updater that applies decisions and saves with a timestamped backup.
- `src/WabbajackPreprocessor.App` — Avalonia GUI: pick a `.compiler_settings` file (or pass it as a command-line argument), Analyze, review the findings in tabs (stale entries / disabled mods / mods without a download / warnings), Apply changes.
- `tests/WabbajackPreprocessor.Core.Tests` — xUnit tests, including an end-to-end analysis over a synthetic MO2 instance.
- `docs/wabbajack-formats.md` — reference for all file formats involved, researched from the Wabbajack 4.2.x and MO2 source code.

Build and run: `dotnet run --project src/WabbajackPreprocessor.App` (requires .NET 9 SDK). Tests: `dotnet test`. 