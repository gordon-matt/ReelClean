namespace ReelClean.Services;

/// <summary>Per-user locations for settings, the TMDB cache, and rename history.</summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ReelClean");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string CacheDirectory => Path.Combine(Root, "cache");

    public static string HistoryDirectory => Path.Combine(Root, "history");
}
