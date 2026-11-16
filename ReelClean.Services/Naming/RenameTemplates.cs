namespace ReelClean.Services.Naming;

public static class RenameTemplates
{
    public const string PlexName = "Plex";

    public const string JellyfinName = "Jellyfin";

    public const string KodiName = "Kodi";

    public const string MinimalName = "Minimal";

    public const string CustomName = "Custom";

    public static IReadOnlyList<string> Names { get; } =
    [
        PlexName,
        JellyfinName,
        KodiName,
        MinimalName,
        CustomName,
    ];

    public static string PatternFor(string? name, string? custom = null) => name switch
    {
        JellyfinName => "{SeriesName} ({Year})/Season {Season:00}/{SeriesName} - S{Season:00}E{Episode:00} - {EpisodeName}",
        KodiName => "{SeriesName}/Season {Season:00}/{SeriesName} - S{Season:00}E{Episode:00} - {EpisodeName}",
        MinimalName => "{SeriesName} - S{Season:00}E{Episode:00}",
        CustomName when !string.IsNullOrWhiteSpace(custom) => custom.Trim(),
        _ => "{SeriesName} - S{Season:00}E{Episode:00} - {EpisodeName}",
    };
}
