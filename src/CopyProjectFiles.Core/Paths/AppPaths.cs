namespace CopyProjectFiles.Core;

/// <summary>
/// Where the app keeps its data on each OS:
///   Windows: %APPDATA%\CopyProjectFiles
///   macOS:   ~/Library/Application Support/CopyProjectFiles
///   Linux:   $XDG_DATA_HOME/CopyProjectFiles (default ~/.local/share/CopyProjectFiles)
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "CopyProjectFiles";
    public const string DatabaseFileName = "app_data.db";

    /// <summary>Folder used by the original Windows console app.</summary>
    public const string LegacyAppFolderName = "ProjectGrabFiles";

    public static string GetDataDirectory()
    {
        string baseDir;

        if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, "Library", "Application Support");
        }
        else
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(baseDir, AppFolderName);
    }

    /// <summary>
    /// Returns the database path, creating the folder if needed. If no database exists yet but
    /// one from the old console app does (…\ProjectGrabFiles\app_data.db), it is copied over
    /// so existing ProjectCopies carry across. The old file is left in place.
    /// </summary>
    public static string GetDatabasePath()
    {
        string dataDir = GetDataDirectory();
        Directory.CreateDirectory(dataDir);
        string dbPath = Path.Combine(dataDir, DatabaseFileName);

        if (!File.Exists(dbPath))
        {
            string legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                LegacyAppFolderName,
                DatabaseFileName);

            if (File.Exists(legacy))
            {
                try { File.Copy(legacy, dbPath); }
                catch (IOException) { /* start fresh if the old file can't be read */ }
                catch (UnauthorizedAccessException) { }
            }
        }

        return dbPath;
    }
}
