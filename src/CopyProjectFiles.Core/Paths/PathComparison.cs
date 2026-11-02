namespace CopyProjectFiles.Core;

/// <summary>
/// How path strings should be compared on the current OS.
/// Windows and macOS (APFS default) file systems are case-insensitive; Linux is case-sensitive,
/// so "Bin" and "bin" are different folders there and must not be treated as the same.
/// </summary>
public static class PathComparison
{
    public static bool IsCaseSensitive { get; } = !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());

    public static StringComparison Comparison { get; } =
        IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static StringComparer StringComparer { get; } =
        IsCaseSensitive ? System.StringComparer.Ordinal : System.StringComparer.OrdinalIgnoreCase;
}
