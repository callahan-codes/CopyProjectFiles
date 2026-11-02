using CopyProjectFiles.Core.Models;

namespace CopyProjectFiles.Core.Services;

public sealed record CopyOptions
{
    /// <summary>True keeps the folder structure; false flattens every file into the destination root.</summary>
    public bool KeepStructure { get; init; }

    /// <summary>True deletes everything already in the destination before copying.</summary>
    public bool ClearDestination { get; init; }
}

/// <param name="RelativePath">'/'-separated path from the project root.</param>
public sealed record PlannedFile(string FullPath, string RelativePath);

/// <summary>What would be copied, computed before anything touches the disk (for a preview/confirm step).</summary>
public sealed record CopyPlan(
    ProjectCopy Project,
    IReadOnlyList<PlannedFile> Files,
    int IgnoredCount,
    IReadOnlyList<string> SkippedFolders)
{
    public long TotalBytes => Files.Sum(f => SafeLength(f.FullPath));

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }
}

public sealed record CopyFailure(string Path, string Message);

public sealed record CopyProgress(int Done, int Total, string CurrentRelativePath);

public sealed record CopyResult(int Copied, int Ignored, IReadOnlyList<CopyFailure> Failures)
{
    public int Failed => Failures.Count;
}

/// <summary>
/// The file work behind "Save project copy to directory", with no console I/O.
/// Usage: <c>var plan = CopyEngine.Plan(project); var result = await CopyEngine.ExecuteAsync(plan, options);</c>
/// </summary>
public static class CopyEngine
{
    /// <summary>Walks the project folder and lists every file that isn't ignored.</summary>
    public static CopyPlan Plan(ProjectCopy project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var files = new List<PlannedFile>();
        var skipped = new List<string>();
        int ignored = 0;
        var pathsToIgnore = project.PathsToIgnore;

        void Walk(string currentPath, string relativePrefix)
        {
            IEnumerable<string> dirs, filesHere;
            try
            {
                dirs = Directory.EnumerateDirectories(currentPath, "*", FileSystemWalk.Directories).ToList();
                filesHere = Directory.EnumerateFiles(currentPath, "*", FileSystemWalk.Files).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                skipped.Add(relativePrefix.Length == 0 ? "." : relativePrefix);
                return;
            }

            foreach (var dir in dirs)
            {
                string relative = IgnoreRules.Combine(relativePrefix, Path.GetFileName(dir));
                if (IgnoreRules.IsIgnored(relative, pathsToIgnore))
                {
                    ignored += CountFiles(dir);
                    continue;
                }
                Walk(dir, relative);
            }

            foreach (var file in filesHere)
            {
                string relative = IgnoreRules.Combine(relativePrefix, Path.GetFileName(file));
                if (IgnoreRules.IsIgnored(relative, pathsToIgnore))
                {
                    ignored++;
                    continue;
                }
                files.Add(new PlannedFile(file, relative));
            }
        }

        if (Directory.Exists(project.Path)) Walk(project.Path, "");
        else skipped.Add(project.Path);

        return new CopyPlan(project, files, ignored, skipped);
    }

    /// <summary>
    /// Copies the planned files into the project's destination. Runs on a background thread,
    /// so it is safe to await from a UI event handler. Validation should already have passed.
    /// </summary>
    public static Task<CopyResult> ExecuteAsync(
        CopyPlan plan,
        CopyOptions options,
        IProgress<CopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => Execute(plan, options, progress, cancellationToken), cancellationToken);
    }

    private static CopyResult Execute(CopyPlan plan, CopyOptions options, IProgress<CopyProgress>? progress, CancellationToken ct)
    {
        var errors = ProjectValidator.Validate(plan.Project);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors.Select(e => e.Message)));

        string destinationRoot = Path.GetFullPath(plan.Project.CopyToPath);
        var failures = new List<CopyFailure>();

        Directory.CreateDirectory(destinationRoot);
        if (options.ClearDestination)
        {
            failures.AddRange(ClearDirectory(destinationRoot));
        }

        int copied = 0;
        int done = 0;
        var usedFlatNames = new HashSet<string>(PathComparison.StringComparer);

        foreach (var file in plan.Files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                string destPath;
                if (options.KeepStructure)
                {
                    destPath = Path.Combine(destinationRoot, IgnoreRules.ToOsPath(file.RelativePath));
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                }
                else
                {
                    destPath = Path.Combine(destinationRoot, UniqueFlatName(Path.GetFileName(file.FullPath), usedFlatNames));
                }

                File.Copy(file.FullPath, destPath, overwrite: true);
                copied++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(new CopyFailure(file.RelativePath, ex.Message));
            }

            done++;
            progress?.Report(new CopyProgress(done, plan.Files.Count, file.RelativePath));
        }

        return new CopyResult(copied, plan.IgnoredCount, failures);
    }

    // "Program.cs", "Program.cs" → "Program.cs", "Program (1).cs"
    private static string UniqueFlatName(string fileName, HashSet<string> used)
    {
        string candidate = fileName;
        int counter = 1;
        while (!used.Add(candidate))
        {
            candidate = $"{Path.GetFileNameWithoutExtension(fileName)} ({counter}){Path.GetExtension(fileName)}";
            counter++;
        }
        return candidate;
    }

    private static int CountFiles(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*", FileSystemWalk.FilesRecursive).Count(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return 0; }
    }

    /// <summary>Deletes everything inside <paramref name="dirPath"/> but keeps the folder itself.</summary>
    private static List<CopyFailure> ClearDirectory(string dirPath)
    {
        var failures = new List<CopyFailure>();
        var dir = new DirectoryInfo(dirPath);
        if (!dir.Exists) return failures;

        foreach (var file in dir.EnumerateFiles("*", FileSystemWalk.Files))
        {
            try { file.Delete(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(new CopyFailure(file.FullName, $"Couldn't delete: {ex.Message}"));
            }
        }

        // include symlinked folders here so the link itself is removed (its target is left alone)
        foreach (var sub in dir.EnumerateDirectories("*", new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = true }))
        {
            try
            {
                if (sub.LinkTarget != null) sub.Delete();
                else sub.Delete(recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(new CopyFailure(sub.FullName, $"Couldn't delete: {ex.Message}"));
            }
        }

        return failures;
    }
}
