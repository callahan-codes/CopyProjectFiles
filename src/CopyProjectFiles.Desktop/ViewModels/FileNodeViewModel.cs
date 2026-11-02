using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CopyProjectFiles.Core.Services;

namespace CopyProjectFiles.Desktop.ViewModels;

/// <summary>
/// One file or folder in the project tree. Folders load their children the first time
/// they're expanded, so huge folders (node_modules, .git) cost nothing until opened.
/// </summary>
public partial class FileNodeViewModel : ViewModelBase
{
    private readonly ProjectEditorViewModel _owner;
    private bool _loaded;

    public FileNodeViewModel(ProjectEditorViewModel owner, FileTreeEntry entry)
    {
        _owner = owner;
        Name = entry.Name;
        RelativePath = entry.RelativePath;
        IsDirectory = entry.IsDirectory;
        SizeText = entry.SizeBytes is long size ? FormatSize(size) : null;

        // placeholder child so the expand arrow shows before the folder is loaded
        if (IsDirectory && FileTreeService.HasChildren(owner.TreeRoot, RelativePath))
            Children.Add(new FileNodeViewModel(owner));
    }

    private FileNodeViewModel(ProjectEditorViewModel owner)
    {
        _owner = owner;
        Name = "Loading…";
        RelativePath = string.Empty;
        IsPlaceholder = true;
    }

    public string Name { get; }
    public string RelativePath { get; }
    public bool IsDirectory { get; }
    public bool IsPlaceholder { get; }
    public string? SizeText { get; }
    public string DisplayName => IsDirectory ? Name + "/" : Name;

    public ObservableCollection<FileNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) EnsureLoaded();
    }

    /// <summary>
    /// Checkbox state: true = copied, false = ignored, null = folder with something ignored inside.
    /// The value the checkbox sends is ignored; clicking always toggles from the current state.
    /// </summary>
    public bool? IsIncluded
    {
        get => IsPlaceholder ? true : _owner.GetInclusion(RelativePath, IsDirectory);
        set
        {
            if (!IsPlaceholder) _owner.ToggleInclusion(this);
        }
    }

    public bool IsIgnored => IsIncluded == false;

    public void EnsureLoaded()
    {
        if (_loaded || !IsDirectory || IsPlaceholder) return;
        _loaded = true;

        Children.Clear();
        foreach (var entry in FileTreeService.GetChildren(_owner.TreeRoot, RelativePath))
        {
            Children.Add(new FileNodeViewModel(_owner, entry));
        }
    }

    /// <summary>Re-reads the checkbox state for this node and all loaded descendants.</summary>
    public void RefreshState()
    {
        OnPropertyChanged(nameof(IsIncluded));
        OnPropertyChanged(nameof(IsIgnored));
        foreach (var child in Children) child.RefreshState();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }
}
