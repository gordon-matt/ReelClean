namespace ReelClean;

public static class Constants
{
    public const int MaxFileNameLength = 180;

    public const int MaxPathLength = 240;

    /// <summary>Ranges wider than this are ignored so a bad parse cannot rename a whole season.</summary>
    public const int MaxEpisodeRange = 20;

    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv",
        ".mp4",
        ".m4v",
        ".avi",
        ".mov",
        ".wmv",
        ".ts",
        ".m2ts",
        ".webm",
        ".mpg",
        ".mpeg",
    };
}
