namespace CopyProjectFiles.Core.Services;

/// <summary>
/// Checkbox-tree edits to an ignore list. Results are always simplified (no entry inside another).
/// </summary>
public static class IgnoreEditor
{
    /// <summary>Unchecking a file or folder: ignore it (and everything inside it).</summary>
    public static List<string> Exclude(IEnumerable<string> pathsToIgnore, string relativePath) =>
        IgnoreRules.Simplify(pathsToIgnore.Append(relativePath));

    /// <summary>
    /// Checking a file or folder: include it and everything inside it. If it sits inside an
    /// ignored folder, that folder is split up so only the path down to this item is re-included
    /// and its siblings stay ignored. For example, with "src" ignored, including "src/a.cs"
    /// gives every other entry under "src" instead.
    /// </summary>
    public static List<string> Include(string projectRoot, IEnumerable<string> pathsToIgnore, string relativePath)
    {
        string target = IgnoreRules.Normalize(relativePath);
        var list = IgnoreRules.Simplify(pathsToIgnore);

        // drop the item itself and anything ignored inside it
        list.RemoveAll(p => p.Equals(target, PathComparison.Comparison)
                         || p.StartsWith(target + "/", PathComparison.Comparison));

        // after Simplify there's at most one ignored ancestor
        string? ancestor = list.FirstOrDefault(p => target.StartsWith(p + "/", PathComparison.Comparison));
        if (ancestor != null)
        {
            list.Remove(ancestor);

            string current = ancestor;
            foreach (var segment in target[(ancestor.Length + 1)..].Split('/'))
            {
                string next = IgnoreRules.Combine(current, segment);
                foreach (var sibling in FileTreeService.GetChildren(projectRoot, current))
                {
                    if (!sibling.RelativePath.Equals(next, PathComparison.Comparison))
                        list.Add(sibling.RelativePath);
                }
                current = next;
            }
        }

        return IgnoreRules.Simplify(list);
    }

    /// <summary>
    /// Checkbox state for a tree item: true = included, false = ignored,
    /// null = folder that is included but has something ignored inside it.
    /// </summary>
    public static bool? GetState(IReadOnlyCollection<string> pathsToIgnore, string relativePath, bool isDirectory)
    {
        if (IgnoreRules.IsIgnored(relativePath, pathsToIgnore)) return false;
        if (isDirectory)
        {
            string prefix = IgnoreRules.Normalize(relativePath) + "/";
            if (pathsToIgnore.Any(p => IgnoreRules.Normalize(p).StartsWith(prefix, PathComparison.Comparison)))
                return null;
        }
        return true;
    }
}
