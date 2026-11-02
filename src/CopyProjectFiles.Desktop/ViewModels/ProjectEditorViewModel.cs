using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyProjectFiles.Core;
using CopyProjectFiles.Core.Data;
using CopyProjectFiles.Core.Models;
using CopyProjectFiles.Core.Services;
using CopyProjectFiles.Desktop.Services;

namespace CopyProjectFiles.Desktop.ViewModels;

/// <summary>
/// One project: an editable draft of its fields, the ignore list, and the copy controls.
/// Edits stay in memory until Save; Copy always runs against the saved version.
/// </summary>
public partial class ProjectEditorViewModel : ViewModelBase
{
    private readonly IProjectRepository _repository;
    private readonly IDesktopServices _desktop;
    private readonly SynchronizationContext? _uiContext;
    private ProjectCopy _saved;
    private bool _loading;
    private bool _isActive;

    public ProjectEditorViewModel(ProjectCopy saved, IProjectRepository repository, IDesktopServices desktop)
    {
        _repository = repository;
        _desktop = desktop;
        _uiContext = SynchronizationContext.Current; // created on the UI thread
        _saved = saved.Clone();
        LoadFrom(_saved);
    }

    public int Id => _saved.Id;
    public bool IsNew => _saved.Id == 0;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(untitled)" : Name.Trim();

    // ---------- details ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private string? _nameError;

    [ObservableProperty]
    private string? _sourceError;

    [ObservableProperty]
    private string? _destinationError;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public string HeaderSubtitle =>
        IsNew ? "New project, not saved yet"
        : HasUnsavedChanges ? "Unsaved changes"
        : $"Saved · ID {Id}";

    // ---------- ignore list ----------

    public ObservableCollection<IgnoreEntryViewModel> IgnoredPaths { get; } = [];

    public bool HasIgnoredPaths => IgnoredPaths.Count > 0;

    public string IgnoredSummary => IgnoredPaths.Count switch
    {
        0 => "Nothing ignored. Every file will be copied.",
        1 => "1 entry ignored",
        int n => $"{n} entries ignored",
    };

    // ---------- live file tree ----------

    /// <summary>Top level of the project folder. Folders load their contents when expanded.</summary>
    public ObservableCollection<FileNodeViewModel> Tree { get; } = [];

    /// <summary>The folder the tree is currently showing.</summary>
    public string TreeRoot { get; private set; } = string.Empty;

    [ObservableProperty]
    private string? _treeMessage = "Choose a project folder above to see its files.";

    private CancellationTokenSource? _treeReloadDelay;
    private FileSystemWatcher? _watcher;
    private string? _watchedRoot;

    // ---------- copy ----------

    /// <summary>Off by default: flattening into one folder is what you want for pasting into Claude.</summary>
    [ObservableProperty]
    private bool _keepStructure;

    [ObservableProperty]
    private bool _clearDestination;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isCopying;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string? _progressText;

    [ObservableProperty]
    private string? _previewSummary;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _statusIsError;

    public string? CopyHint =>
        IsNew ? "Save the project before copying."
        : HasUnsavedChanges ? "Save or revert your changes before copying."
        : !IsValid ? "Fix the errors above before copying."
        : null;

    partial void OnNameChanged(string value) => Revalidate();
    partial void OnSourcePathChanged(string value)
    {
        Revalidate();
        ScheduleTreeReload(); // debounced so typing a path doesn't reload on every key
    }
    partial void OnDestinationPathChanged(string value) => Revalidate();

    // ---------- commands: details ----------

    private bool CanSave() => IsValid && HasUnsavedChanges && !IsCopying;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var model = ToModel();
        try
        {
            if (IsNew)
            {
                await _repository.AddAsync(model);
            }
            else if (!await _repository.UpdateAsync(model))
            {
                SetStatus("This project no longer exists in the database.", isError: true);
                return;
            }

            _saved = model.Clone();
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(IsNew));
            LoadFrom(_saved);
            SetStatus($"Saved '{DisplayName}'.");
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't save: {ex.Message}", isError: true);
        }
    }

    private bool CanRevert() => HasUnsavedChanges && !IsNew;

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void Revert()
    {
        LoadFrom(_saved);
        SetStatus("Changes reverted.");
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        string? path = await _desktop.PickFolderAsync("Choose the project folder", SourcePath);
        if (path is not null) SourcePath = path;
    }

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        string? path = await _desktop.PickFolderAsync("Choose where the copy goes", DestinationPath);
        if (path is not null) DestinationPath = path;
    }

    // ---------- ignore list + tree ----------

    /// <summary>Called by the main window when this project becomes / stops being the selected one.</summary>
    public void Activate()
    {
        _isActive = true;
        ReloadTree();
    }

    public void Deactivate()
    {
        _isActive = false;
        _treeReloadDelay?.Cancel();
        StopWatcher();
    }

    [RelayCommand]
    private void RefreshTree() => ReloadTree();

    [RelayCommand]
    private void ExpandAll()
    {
        // one level at a time, so this stays fast; click again to go deeper
        foreach (var node in Tree.Where(n => n.IsDirectory)) ExpandLoaded(node);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in Tree) Collapse(node);
    }

    internal bool? GetInclusion(string relativePath, bool isDirectory) =>
        IgnoreEditor.GetState(IgnoredPaths.Select(i => i.Path).ToList(), relativePath, isDirectory);

    internal void ToggleInclusion(FileNodeViewModel node)
    {
        var current = IgnoredPaths.Select(i => i.Path).ToList();
        var updated = GetInclusion(node.RelativePath, node.IsDirectory) == true
            ? IgnoreEditor.Exclude(current, node.RelativePath)
            : IgnoreEditor.Include(TreeRoot, current, node.RelativePath); // partial or ignored → include all
        SetIgnoredPaths(updated);
    }

    private void RemoveIgnore(IgnoreEntryViewModel entry)
    {
        IgnoredPaths.Remove(entry);
        Revalidate();
        RefreshTreeState();
    }

    private async void ScheduleTreeReload(int delayMs = 300)
    {
        if (!_isActive) return;
        _treeReloadDelay?.Cancel();
        var cts = _treeReloadDelay = new CancellationTokenSource();
        try
        {
            await Task.Delay(delayMs, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (_isActive) ReloadTree();
    }

    private void ReloadTree()
    {
        var expanded = new HashSet<string>(PathComparison.StringComparer);
        CollectExpanded(Tree, expanded);
        string root = SourcePath.Trim();
        bool sameRoot = root.Equals(TreeRoot, PathComparison.Comparison);

        Tree.Clear();
        TreeRoot = root;

        if (root.Length == 0 || !Directory.Exists(root))
        {
            TreeMessage = root.Length == 0
                ? "Choose a project folder above to see its files."
                : "That folder doesn't exist.";
            StopWatcher();
            return;
        }

        var entries = FileTreeService.GetChildren(root);
        foreach (var entry in entries)
        {
            Tree.Add(new FileNodeViewModel(this, entry));
        }
        if (sameRoot) Reexpand(Tree, expanded); // keep folders open across live refreshes

        TreeMessage = entries.Count == 0 ? "This folder is empty." : null;
        StartWatcher(root);
    }

    private static void CollectExpanded(IEnumerable<FileNodeViewModel> nodes, HashSet<string> into)
    {
        foreach (var node in nodes.Where(n => n.IsExpanded))
        {
            into.Add(node.RelativePath);
            CollectExpanded(node.Children, into);
        }
    }

    private static void Reexpand(IEnumerable<FileNodeViewModel> nodes, HashSet<string> expanded)
    {
        foreach (var node in nodes.Where(n => n.IsDirectory && expanded.Contains(n.RelativePath)))
        {
            node.IsExpanded = true; // loads children
            Reexpand(node.Children, expanded);
        }
    }

    private static void ExpandLoaded(FileNodeViewModel node)
    {
        if (!node.IsExpanded)
        {
            node.IsExpanded = true;
            return;
        }
        foreach (var child in node.Children.Where(c => c.IsDirectory)) ExpandLoaded(child);
    }

    private static void Collapse(FileNodeViewModel node)
    {
        foreach (var child in node.Children) Collapse(child);
        node.IsExpanded = false;
    }

    // Live updates: watch for files/folders being added, removed or renamed.
    private void StartWatcher(string root)
    {
        if (_watcher != null && root.Equals(_watchedRoot, PathComparison.Comparison)) return;
        StopWatcher();

        try
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            };
            watcher.Created += OnFileSystemChanged;
            watcher.Deleted += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemChanged;
            watcher.EnableRaisingEvents = true;

            _watcher = watcher;
            _watchedRoot = root;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // e.g. Linux inotify watch limit on very large trees: the Refresh button still works
            _watcher = null;
            _watchedRoot = null;
        }
    }

    private void StopWatcher()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileSystemChanged;
        _watcher.Deleted -= OnFileSystemChanged;
        _watcher.Renamed -= OnFileSystemChanged;
        _watcher.Dispose();
        _watcher = null;
        _watchedRoot = null;
    }

    // raised on a background thread; hop to the UI thread and debounce bursts (e.g. a build writing bin/obj)
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e) =>
        PostToUi(() => ScheduleTreeReload(delayMs: 500));

    private void RefreshTreeState()
    {
        // posted so a checkbox click finishes before its state is re-read
        PostToUi(() =>
        {
            foreach (var node in Tree) node.RefreshState();
        });
    }

    private void PostToUi(Action action)
    {
        if (_uiContext is null) action();
        else _uiContext.Post(_ => action(), null);
    }

    // ---------- commands: copy ----------

    private bool CanRun() => !IsNew && !HasUnsavedChanges && IsValid && !IsCopying;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task PreviewAsync()
    {
        var project = _saved.Clone();
        try
        {
            var (plan, bytes) = await Task.Run(() =>
            {
                var p = CopyEngine.Plan(project);
                return (p, p.TotalBytes);
            });

            string summary = $"{plan.Files.Count} files ({FormatSize(bytes)}) will be copied · {plan.IgnoredCount} ignored";
            if (plan.SkippedFolders.Count > 0) summary += $" · {plan.SkippedFolders.Count} unreadable folder(s) skipped";
            PreviewSummary = summary;
        }
        catch (Exception ex)
        {
            SetStatus($"Preview failed: {ex.Message}", isError: true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun), IncludeCancelCommand = true)]
    private async Task CopyAsync(CancellationToken token)
    {
        IsCopying = true;
        ProgressValue = 0;
        ProgressText = null;
        StatusMessage = null;

        try
        {
            var project = _saved.Clone();
            var plan = await Task.Run(() => CopyEngine.Plan(project), token);
            if (plan.Files.Count == 0)
            {
                SetStatus("No files to copy after applying the ignore list.", isError: true);
                return;
            }

            // Progress<T> raises on the UI thread because it's created here
            var progress = new Progress<CopyProgress>(p =>
            {
                ProgressValue = p.Total == 0 ? 100 : 100.0 * p.Done / p.Total;
                ProgressText = p.CurrentRelativePath;
            });

            var options = new CopyOptions { KeepStructure = KeepStructure, ClearDestination = ClearDestination };
            var result = await CopyEngine.ExecuteAsync(plan, options, progress, token);

            ProgressValue = 100;
            string message = $"Copied {result.Copied} files · {result.Ignored} ignored · {result.Failed} failed.";
            if (result.Failed > 0)
            {
                var first = result.Failures[0];
                message += $" First error: {first.Path}: {first.Message}";
            }
            SetStatus(message, isError: result.Failed > 0);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Copy cancelled. Files already copied were left in place.", isError: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Copy failed: {ex.Message}", isError: true);
        }
        finally
        {
            IsCopying = false;
            ProgressText = null;
        }
    }

    [RelayCommand]
    private async Task OpenDestinationAsync()
    {
        if (!Directory.Exists(DestinationPath))
        {
            SetStatus("The destination folder doesn't exist yet. It's created on the first copy.", isError: true);
            return;
        }
        if (!await _desktop.OpenFolderAsync(DestinationPath))
        {
            SetStatus("Couldn't open the folder.", isError: true);
        }
    }

    // ---------- helpers ----------

    public ProjectCopy ToModel() => new()
    {
        Id = _saved.Id,
        Name = Name.Trim(),
        Path = SourcePath.Trim(),
        CopyToPath = DestinationPath.Trim(),
        PathsToIgnore = IgnoredPaths.Select(i => i.Path).ToList(),
    };

    private void LoadFrom(ProjectCopy project)
    {
        _loading = true;
        Name = project.Name;
        SourcePath = project.Path;
        DestinationPath = project.CopyToPath;
        _loading = false;
        SetIgnoredPaths(project.PathsToIgnore);
    }

    private void SetIgnoredPaths(IEnumerable<string> paths)
    {
        IgnoredPaths.Clear();
        foreach (var path in paths)
        {
            IgnoredPaths.Add(new IgnoreEntryViewModel(path, RemoveIgnore));
        }
        Revalidate();
        RefreshTreeState();
    }

    private void Revalidate()
    {
        if (_loading) return;

        var model = ToModel();
        var errors = ProjectValidator.Validate(model);
        NameError = errors.FirstOrDefault(e => e.Field == ProjectField.Name)?.Message;
        SourceError = errors.FirstOrDefault(e => e.Field == ProjectField.Path)?.Message;
        DestinationError = errors.FirstOrDefault(e => e.Field == ProjectField.CopyToPath)?.Message;
        IsValid = errors.Count == 0;
        HasUnsavedChanges = IsNew || !SameAs(model, _saved);
        PreviewSummary = null;

        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(CopyHint));
        OnPropertyChanged(nameof(HasIgnoredPaths));
        OnPropertyChanged(nameof(IgnoredSummary));
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
    }

    private static bool SameAs(ProjectCopy a, ProjectCopy b) =>
        a.Name.Trim() == b.Name.Trim()
        && a.Path.Trim() == b.Path.Trim()
        && a.CopyToPath.Trim() == b.CopyToPath.Trim()
        && a.PathsToIgnore.ToHashSet(PathComparison.StringComparer).SetEquals(b.PathsToIgnore); // order doesn't matter

    private void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        StatusIsError = isError;
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
