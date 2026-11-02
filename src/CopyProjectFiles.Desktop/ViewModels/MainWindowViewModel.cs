using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyProjectFiles.Core.Data;
using CopyProjectFiles.Core.Models;
using CopyProjectFiles.Desktop.Services;

namespace CopyProjectFiles.Desktop.ViewModels;

/// <summary>Left-hand project list plus New / Delete.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectRepository _repository;
    private readonly IDesktopServices _desktop;

    public MainWindowViewModel(IProjectRepository repository, IDesktopServices desktop)
    {
        _repository = repository;
        _desktop = desktop;
    }

    public ObservableCollection<ProjectEditorViewModel> Projects { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(DeletePrompt))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private ProjectEditorViewModel? _selectedProject;

    [ObservableProperty]
    private bool _isConfirmingDelete;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasSelection => SelectedProject is not null;

    public string DeletePrompt => SelectedProject is null
        ? string.Empty
        : $"Delete '{SelectedProject.DisplayName}'? This removes the saved project only; no files are deleted.";

    public async Task LoadAsync()
    {
        try
        {
            var all = await _repository.GetAllAsync();
            Projects.Clear();
            foreach (var project in all)
            {
                Projects.Add(CreateEditor(project));
            }
            SelectedProject = Projects.FirstOrDefault();
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't open the database: {ex.Message}";
        }
    }

    [RelayCommand]
    private void New()
    {
        var editor = CreateEditor(new ProjectCopy());
        Projects.Add(editor);
        SelectedProject = editor;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete() => IsConfirmingDelete = true;

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        var target = SelectedProject;
        IsConfirmingDelete = false;
        if (target is null) return;

        try
        {
            if (!target.IsNew) await _repository.DeleteAsync(target.Id);

            int index = Projects.IndexOf(target);
            Projects.Remove(target);
            SelectedProject = Projects.Count == 0 ? null : Projects[Math.Min(index, Projects.Count - 1)];
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    // only the visible project shows a live file tree / file watcher
    partial void OnSelectedProjectChanged(ProjectEditorViewModel? oldValue, ProjectEditorViewModel? newValue)
    {
        IsConfirmingDelete = false;
        oldValue?.Deactivate();
        newValue?.Activate();
    }

    private ProjectEditorViewModel CreateEditor(ProjectCopy project) =>
        new(project, _repository, _desktop);
}
