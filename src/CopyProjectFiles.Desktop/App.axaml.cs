using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CopyProjectFiles.Core.Data;
using CopyProjectFiles.Desktop.Services;
using CopyProjectFiles.Desktop.ViewModels;
using CopyProjectFiles.Desktop.Views;

namespace CopyProjectFiles.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var services = new DesktopServices(() => window);
            var repository = new SqliteProjectRepository(); // per-OS data folder, see AppPaths
            var viewModel = new MainWindowViewModel(repository, services);

            window.DataContext = viewModel;
            window.Opened += async (_, _) => await viewModel.LoadAsync();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
