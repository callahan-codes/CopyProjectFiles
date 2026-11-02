namespace CopyProjectFiles.Core.Services;

/// <summary>
/// Shared enumeration settings. Unlike the defaults for EnumerationOptions, hidden files
/// (.gitignore, .env, …) are included, matching the original console behavior. Symlinked
/// folders are skipped to avoid infinite loops on macOS/Linux, and unreadable entries are skipped.
/// </summary>
internal static class FileSystemWalk
{
    public static readonly EnumerationOptions Directories = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public static readonly EnumerationOptions Files = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
    };

    public static readonly EnumerationOptions FilesRecursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };
}
