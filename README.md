# CopyProjectFiles (v2, console)

A Windows console app that copies a project's files into a separate folder, without the files you've chosen to leave out. The copy can be flattened into one folder, which makes it easy to drag or paste into Claude (or any AI tool) without giving it access to your real project or to large or sensitive files.

> **Looking for the desktop app?** The cross-platform GUI version (Windows, macOS and Linux) is on the [`ui`](../../tree/ui) branch. This `console` branch holds the original command-line version.

## Features

- Save any number of **ProjectCopies**, each with a name, a source folder, a destination folder and a list of ignored paths
- Browse the project folder in the terminal, open and close subfolders, and pick files or folders to ignore
- Ignoring a folder skips everything inside it
- When saving a copy, choose to **keep the folder structure** or **flatten** every file into one folder. Duplicate names are renamed automatically, e.g. `Program (1).cs`
- Optionally **clear the destination folder** before copying
- Projects are stored in a local SQLite database

## Requirements

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

```powershell
dotnet run
```

Or build an executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

## Usage

The main menu lists your saved projects and these options:

| Key | Action |
|-----|--------|
| `1` | Create new project copy |
| `2` | Edit existing project copy details |
| `3` | Delete existing project copy |
| `4` | Save project copy to directory |
| `5` | Exit |

### Creating or editing a project

You're asked for a **name**, the **project folder** to copy from, and the **destination folder** to copy into. Both folders must exist, and the destination can't be the same as the project folder.

Next, the project's contents are shown so you can choose what to ignore:

| Input | What it does |
|-------|--------------|
| `bin obj .env` | Ignore these items (separate several with spaces) |
| `folder1/bin` | Ignore a specific path when a name appears in more than one place |
| `open src` / `open src/lib` | Expand a folder (use `/` for nested folders) |
| `close src` | Collapse a folder |
| `untrack bin` | Include a previously ignored item again |
| `--c` | Continue |

At any prompt:

| Input | What it does |
|-------|--------------|
| `--b` | Go back to the previous step |
| `--x` | Cancel and return to the main menu |

Every create, edit, delete and save asks for a `y`/`n` confirmation first.

### Saving a copy

Pick a project by ID, then answer:

1. **Keep the original directory structure?** `y` mirrors the folders, `n` flattens every file into the destination root.
2. **Clear the destination first?** `y` deletes what's already in the destination folder before copying.

When it's done, you'll see how many files were copied, ignored and failed.

## Data

Saved projects are stored in:

```
%APPDATA%\ProjectGrabFiles\app_data.db
```

The `ui` version automatically imports this database the first time it runs.

## Project structure

```
Program.cs                      Main menu loop
Assets/
  Interfaces/                   IDbService, IMenu, IProjectService
  Menu/                         Menu screens (base, create, edit, delete, save)
  Models/ProjectCopy.cs         Saved project (SQLite table)
  Services/
    DbService.cs                Database location and setup
    ProjectService.cs           Create / edit / delete / save logic
```