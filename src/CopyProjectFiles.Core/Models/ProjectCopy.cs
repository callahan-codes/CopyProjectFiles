using System.Text.Json;
using SQLite;

namespace CopyProjectFiles.Core.Models;

/// <summary>
/// A saved "copy job": copy files from <see cref="Path"/> into <see cref="CopyToPath"/>,
/// skipping anything listed in <see cref="PathsToIgnore"/>.
/// Column names match the original console app, so existing databases load unchanged.
/// </summary>
public class ProjectCopy
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Source project folder.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Destination folder the copy is written to.</summary>
    public string CopyToPath { get; set; } = string.Empty;

    /// <summary>Kept for compatibility with the v2 schema. Updated automatically.</summary>
    public int NumberOfPathsToIgnore { get; set; }

    private string _pathsToIgnoreJson = "[]";
    private IReadOnlyList<string>? _pathsToIgnoreCache;

    /// <summary>Stored column (TEXT). Use <see cref="PathsToIgnore"/> instead.</summary>
    public string PathsToIgnoreJson
    {
        get => _pathsToIgnoreJson;
        set
        {
            _pathsToIgnoreJson = string.IsNullOrWhiteSpace(value) ? "[]" : value;
            _pathsToIgnoreCache = null;
        }
    }

    /// <summary>
    /// Relative paths (always '/'-separated, e.g. "bin" or "src/obj") excluded from the copy.
    /// Read-only: assign a new list to change it, so the JSON column always stays in sync.
    /// </summary>
    [Ignore]
    public IReadOnlyList<string> PathsToIgnore
    {
        get => _pathsToIgnoreCache ??= Deserialize(_pathsToIgnoreJson);
        set
        {
            var normalized = (value ?? [])
                .Select(IgnoreRules.Normalize)
                .Where(p => p.Length > 0)
                .Distinct(PathComparison.StringComparer)
                .ToList();

            _pathsToIgnoreJson = JsonSerializer.Serialize(normalized);
            _pathsToIgnoreCache = normalized;
            NumberOfPathsToIgnore = normalized.Count;
        }
    }

    public ProjectCopy Clone() => new()
    {
        Id = Id,
        Name = Name,
        Path = Path,
        CopyToPath = CopyToPath,
        NumberOfPathsToIgnore = NumberOfPathsToIgnore,
        PathsToIgnoreJson = PathsToIgnoreJson,
    };

    private static IReadOnlyList<string> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
