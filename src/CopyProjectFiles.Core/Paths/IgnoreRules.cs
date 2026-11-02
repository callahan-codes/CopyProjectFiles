namespace CopyProjectFiles.Core;

/// <summary>
/// Rules for the ignore list. Ignored entries are relative paths using '/' on every OS,
/// so a saved project behaves the same no matter which machine it was created on.
/// </summary>
public static class IgnoreRules
{
    /// <summary>Converts "src\obj\" or "/src/obj" to "src/obj".</summary>
    public static string Normalize(string relativePath) =>
        (relativePath ?? string.Empty).Replace('\\', '/').Trim().Trim('/');

    /// <summary>True if <paramref name="relativePath"/> is ignored itself or sits inside an ignored folder.</summary>
    public static bool IsIgnored(string relativePath, IEnumerable<string> pathsToIgnore)
    {
        string path = Normalize(relativePath);
        foreach (var raw in pathsToIgnore)
        {
            string ignored = Normalize(raw);
            if (ignored.Length == 0) continue;
            if (path.Equals(ignored, PathComparison.Comparison)) return true;
            if (path.StartsWith(ignored + "/", PathComparison.Comparison)) return true;
        }
        return false;
    }

    /// <summary>
    /// Removes entries already covered by an ignored parent folder
    /// (e.g. "bin" makes "bin/Debug" redundant). Useful after checkbox-tree edits.
    /// </summary>
    public static List<string> Simplify(IEnumerable<string> pathsToIgnore)
    {
        var normalized = pathsToIgnore
            .Select(Normalize)
            .Where(p => p.Length > 0)
            .Distinct(PathComparison.StringComparer)
            .OrderBy(p => p.Length)
            .ToList();

        var result = new List<string>();
        foreach (var path in normalized)
        {
            if (!IsIgnored(path, result)) result.Add(path);
        }
        return result;
    }

    /// <summary>Joins a relative parent and a child name: ("", "a") → "a", ("a", "b") → "a/b".</summary>
    public static string Combine(string relativeParent, string name)
    {
        string parent = Normalize(relativeParent);
        return parent.Length == 0 ? name : $"{parent}/{name}";
    }

    /// <summary>Converts a '/'-separated relative path to the current OS separator.</summary>
    public static string ToOsPath(string relativePath) =>
        Normalize(relativePath).Replace('/', System.IO.Path.DirectorySeparatorChar);
}
