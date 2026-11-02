# CopyProjectFiles

Cross-platform rewrite of the CopyProjectFiles console app (Windows / macOS / Linux).

```
CopyProjectFiles.sln
src/
  CopyProjectFiles.Core/        no UI code; shared by every frontend
    Models/ProjectCopy.cs       same DB columns as v2, so existing data loads
    Data/                       IProjectRepository + SQLite (async) implementation
    Paths/                      AppPaths (per-OS data folder), IgnoreRules, PathComparison
    Services/
      ProjectValidator.cs       field errors instead of console re-prompts
      CopyEngine.cs             Plan() → preview, ExecuteAsync() → copy with progress
      FileTreeService.cs        lazy folder listing for the checkbox tree
      IgnoreEditor.cs           checkbox include/exclude rules for the ignore list
  CopyProjectFiles.Desktop/     Avalonia 11 + CommunityToolkit.Mvvm GUI
    ViewModels/                 MainWindowViewModel (list), ProjectEditorViewModel (form + copy)
    Views/                      MainWindow, ProjectEditorView
    Services/DesktopServices.cs native folder picker, open folder in Explorer/Finder
tests/
  CopyProjectFiles.Core.Tests/  xUnit
```

Run the app: `dotnet run --project src/CopyProjectFiles.Desktop`
Run the tests: `dotnet test`

Build a standalone app for each OS:

```
dotnet publish src/CopyProjectFiles.Desktop -c Release -r win-x64   --self-contained
dotnet publish src/CopyProjectFiles.Desktop -c Release -r osx-arm64 --self-contained
dotnet publish src/CopyProjectFiles.Desktop -c Release -r linux-x64 --self-contained
```

## Where the old console code went

| v2 (console)                                      | Core                                         |
|---------------------------------------------------|----------------------------------------------|
| `DbService` + `ProjectService` DB methods          | `SqliteProjectRepository`                    |
| `ValidateInput` loop                               | `ProjectValidator.Validate`                  |
| `CollectFilesToCopy` / `CollectFilesRecursive`     | `CopyEngine.Plan`                            |
| `PerformCopy` / `ClearDirectory`                   | `CopyEngine.ExecuteAsync` + `CopyOptions`    |
| `IsPathIgnored`, `ResolveIgnoreToken`, `untrack`   | `IgnoreRules`                                |
| `open` / `close` + `DisplayDirectoryWithExpansions`| `FileTreeService.GetChildren`                |
| Menus, `EnsureTrigger`, `HandleTerminalReset`      | removed: these become the Avalonia UI        |

## Behavior changes

- Targets `net10.0` (was `net10.0-windows`).
- Path matching is case-sensitive on Linux and case-insensitive on Windows and macOS.
- Data folder: `%APPDATA%\CopyProjectFiles`, `~/Library/Application Support/CopyProjectFiles`,
  or `~/.local/share/CopyProjectFiles`. On first run, the old `ProjectGrabFiles\app_data.db` is copied over.
- The destination can't be the project folder, inside it, or a parent of it.
  A parent destination combined with "clear destination" would delete the project.
- Symlinked folders are skipped (no infinite loops), unreadable folders are skipped and reported,
  and hidden files like `.env` are still included unless ignored.
