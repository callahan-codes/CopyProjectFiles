using CopyProjectFiles.Core.Models;

namespace CopyProjectFiles.Core.Data;

/// <summary>
/// Storage for ProjectCopies. Methods throw on database errors; the UI decides how to show them.
/// </summary>
public interface IProjectRepository
{
    Task InitializeAsync();
    Task<List<ProjectCopy>> GetAllAsync();
    Task<ProjectCopy?> GetAsync(int id);

    /// <summary>Inserts the project and sets its <see cref="ProjectCopy.Id"/>.</summary>
    Task<ProjectCopy> AddAsync(ProjectCopy projectCopy);

    /// <returns>False if no project with that Id exists.</returns>
    Task<bool> UpdateAsync(ProjectCopy projectCopy);

    /// <returns>False if no project with that Id exists.</returns>
    Task<bool> DeleteAsync(int id);
}
