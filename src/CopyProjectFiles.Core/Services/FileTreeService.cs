namespace CopyProjectFiles.Core.Services;

/// <summary>One file or folder in a project, for display in the checkbox tree.</summary>
/// <param name="RelativePath">'/'-separated path from the project root, e.g. "src/obj".</param>
/// <param name="SizeBytes">File size; null for folders.</param>
public sealed record FileTreeEntry(string Name, string RelativePath, bool IsDirectory, long? SizeBytes);

/// <summary>
/// Lists a project's contents one level at a time, so the UI can load folders lazily
/// when the user expands them (replaces the console "open"/"close" commands).
/// </summary>
public static class FileTreeService
{
    /// <summary>
    /// Children of <paramref name="relativeFolder"/> ("" for the root): folders first, then files,
    /// each sorted by name. Returns an empty list if the folder is missing or unreadable.
    /// </summary>
    public static List<FileTreeEntry> GetChildren(string projectRoot, string relativeFolder = "")
    {
        string folder = Path.Combine(projectRoot, IgnoreRules.ToOsPath(relativeFolder));
        var result = new List<FileTreeEntry>();
        if (!Directory.Exists(folder)) return result;

        try
        {
            var dirs = new DirectoryInfo(folder).EnumerateDirectories("*", FileSystemWalk.Directories)
                .OrderBy(d => d.Name, PathComparison.StringComparer);
            foreach (var dir in dirs)
            {
                result.Add(new FileTreeEntry(dir.Name, IgnoreRules.Combine(relativeFolder, dir.Name), true, null));
            }

            var files = new DirectoryInfo(folder).EnumerateFiles("*", FileSystemWalk.Files)
                .OrderBy(f => f.Name, PathComparison.StringComparer);
            foreach (var file in files)
            {
                long? size = null;
                try { size = file.Length; } catch (IOException) { }
                result.Add(new FileTreeEntry(file.Name, IgnoreRules.Combine(relativeFolder, file.Name), false, size));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // unreadable folder: show it as empty
        }

        return result;
    }

    /// <summary>True if the folder has at least one child (so the UI can show an expand arrow).</summary>
    public static bool HasChildren(string projectRoot, string relativeFolder)
    {
        string folder = Path.Combine(projectRoot, IgnoreRules.ToOsPath(relativeFolder));
        try
        {
            return Directory.EnumerateFileSystemEntries(folder, "*", FileSystemWalk.Files).Any();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
