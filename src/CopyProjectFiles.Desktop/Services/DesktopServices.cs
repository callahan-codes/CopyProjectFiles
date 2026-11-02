using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CopyProjectFiles.Desktop.Services;

/// <summary>OS integration the view models need, kept behind an interface so they stay UI-free.</summary>
public interface IDesktopServices
{
    /// <summary>Shows the native folder picker. Returns null if cancelled.</summary>
    Task<string?> PickFolderAsync(string title, string? startPath);

    /// <summary>Opens a folder in Explorer / Finder / the Linux file manager.</summary>
    Task<bool> OpenFolderAsync(string path);
}

public sealed class DesktopServices(Func<TopLevel?> getTopLevel) : IDesktopServices
{
    public async Task<string?> PickFolderAsync(string title, string? startPath)
    {
        var storage = getTopLevel()?.StorageProvider;
        if (storage is null || !storage.CanPickFolder) return null;

        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
        {
            try { start = await storage.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(startPath))); }
            catch { /* fall back to the OS default location */ }
        }

        var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<bool> OpenFolderAsync(string path)
    {
        var launcher = getTopLevel()?.Launcher;
        if (launcher is null || !Directory.Exists(path)) return false;
        return await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
    }
}
