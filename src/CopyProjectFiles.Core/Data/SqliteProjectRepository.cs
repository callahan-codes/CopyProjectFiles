using CopyProjectFiles.Core.Models;
using SQLite;

namespace CopyProjectFiles.Core.Data;

/// <summary>SQLite-backed repository. Works unchanged on Windows, macOS and Linux.</summary>
public sealed class SqliteProjectRepository : IProjectRepository, IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    /// <param name="databasePath">Defaults to <see cref="AppPaths.GetDatabasePath"/>.</param>
    public SqliteProjectRepository(string? databasePath = null)
    {
        DatabasePath = databasePath ?? AppPaths.GetDatabasePath();
        _db = new SQLiteAsyncConnection(DatabasePath);
    }

    public string DatabasePath { get; }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<ProjectCopy>();
        _initialized = true;
    }

    public async Task<List<ProjectCopy>> GetAllAsync()
    {
        await InitializeAsync();
        return await _db.Table<ProjectCopy>().OrderBy(p => p.Id).ToListAsync();
    }

    public async Task<ProjectCopy?> GetAsync(int id)
    {
        await InitializeAsync();
        return await _db.FindAsync<ProjectCopy>(id);
    }

    public async Task<ProjectCopy> AddAsync(ProjectCopy projectCopy)
    {
        ArgumentNullException.ThrowIfNull(projectCopy);
        await InitializeAsync();
        await _db.InsertAsync(projectCopy); // sets Id via AutoIncrement
        return projectCopy;
    }

    public async Task<bool> UpdateAsync(ProjectCopy projectCopy)
    {
        ArgumentNullException.ThrowIfNull(projectCopy);
        await InitializeAsync();
        return await _db.UpdateAsync(projectCopy) > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await InitializeAsync();
        return await _db.DeleteAsync<ProjectCopy>(id) > 0;
    }

    public async ValueTask DisposeAsync() => await _db.CloseAsync();
}
