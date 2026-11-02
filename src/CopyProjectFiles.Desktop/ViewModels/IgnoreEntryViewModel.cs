using CommunityToolkit.Mvvm.Input;

namespace CopyProjectFiles.Desktop.ViewModels;

/// <summary>One row in the ignore list, with its own Remove button.</summary>
public partial class IgnoreEntryViewModel : ViewModelBase
{
    private readonly Action<IgnoreEntryViewModel> _remove;

    public IgnoreEntryViewModel(string path, Action<IgnoreEntryViewModel> remove)
    {
        Path = path;
        _remove = remove;
    }

    /// <summary>'/'-separated relative path, e.g. "bin" or "src/obj".</summary>
    public string Path { get; }

    [RelayCommand]
    private void Remove() => _remove(this);
}
