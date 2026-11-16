namespace ReelClean.Services;

public static class PathComparison
{
    /// <summary>
    /// Windows and macOS are usually case-insensitive. Linux is not.
    /// Callers still check the real filesystem before writing.
    /// </summary>
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public static string Full(string path) => Path.GetFullPath(path);

    public static bool Equal(string left, string right) =>
        Comparer.Equals(Full(left), Full(right));

    /// <summary>Ordinal comparison, so a case-only rename is not treated as a no-op.</summary>
    public static bool SameExactPath(string left, string right) =>
        string.Equals(Full(left), Full(right), StringComparison.Ordinal);

    public static bool IsInside(string root, string path)
    {
        string fullRoot = Full(root);
        string fullPath = Full(path);
        if (Equal(fullRoot, fullPath))
        {
            return true;
        }

        string prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var comparison = Comparer == StringComparer.OrdinalIgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(prefix, comparison);
    }
}
