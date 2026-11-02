using CopyProjectFiles.Core.Models;

namespace CopyProjectFiles.Core.Services;

public enum ProjectField { Name, Path, CopyToPath }

public sealed record ValidationError(ProjectField Field, string Message);

/// <summary>
/// Checks a ProjectCopy before it is saved or run. Replaces the console ValidateInput loop:
/// the UI shows each error next to its field instead of re-prompting.
/// </summary>
public static class ProjectValidator
{
    public static List<ValidationError> Validate(ProjectCopy project)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(project.Name))
            errors.Add(new(ProjectField.Name, "Name is required."));

        string? source = TryFullPath(project.Path);
        if (string.IsNullOrWhiteSpace(project.Path))
            errors.Add(new(ProjectField.Path, "Project folder is required."));
        else if (source == null || !Directory.Exists(source))
            errors.Add(new(ProjectField.Path, $"Folder doesn't exist: '{project.Path}'."));

        string? dest = TryFullPath(project.CopyToPath);
        if (string.IsNullOrWhiteSpace(project.CopyToPath))
        {
            errors.Add(new(ProjectField.CopyToPath, "Destination folder is required."));
        }
        else if (dest == null)
        {
            errors.Add(new(ProjectField.CopyToPath, $"Invalid path: '{project.CopyToPath}'."));
        }
        else if (File.Exists(dest))
        {
            errors.Add(new(ProjectField.CopyToPath, "Destination is a file, not a folder."));
        }
        else if (source != null)
        {
            if (IsSameOrInside(dest, source))
            {
                // copying into the project would bloat it with copies (and recurse on later runs)
                errors.Add(new(ProjectField.CopyToPath,
                    "Destination can't be the project folder or inside it."));
            }
            else if (IsSameOrInside(source, dest))
            {
                // "clear destination" would delete the project itself
                errors.Add(new(ProjectField.CopyToPath,
                    "Destination can't be a parent of the project folder."));
            }
        }

        return errors;
    }

    /// <summary>True if <paramref name="path"/> equals <paramref name="folder"/> or is nested inside it.</summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        string p = Trim(Path.GetFullPath(path));
        string f = Trim(Path.GetFullPath(folder));
        if (p.Equals(f, PathComparison.Comparison)) return true;
        string prefix = f.EndsWith(Path.DirectorySeparatorChar) ? f : f + Path.DirectorySeparatorChar;
        return p.StartsWith(prefix, PathComparison.Comparison);
    }

    private static string Trim(string fullPath)
    {
        // keep a bare root like "/" or "C:\" intact
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        return fullPath.Length > root.Length
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }

    private static string? TryFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.GetFullPath(path.Trim()); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
