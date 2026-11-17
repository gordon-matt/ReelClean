namespace ReelClean.Services.Naming;

public static class RenameTemplates
{
    public const string PlexName = "Plex";

    public const string JellyfinName = "Jellyfin";

    public const string KodiName = "Kodi";

    public const string MinimalName = "Minimal";

    public const string CustomName = "Custom";

    public static IReadOnlyList<TemplateToken> Tokens { get; } =
    [
        new("{SeriesName}", "Series title"),
        new("{Year}", "First air year"),
        new("{Season:00}", "Season number"),
        new("{Episode:00}", "Episode number"),
        new("{EpisodeName}", "Episode title"),
        new("{AirDate}", "Air date"),
        new("{OriginalFilename}", "Original file name"),
        new("{Extension}", "File extension"),
        new("S{Season:00}E{Episode:00}", "Season and episode together, such as S01E04"),
        new("/", "Folder separator"),
    ];

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
        MinimalName => "S{Season:00}E{Episode:00} - {EpisodeName}",
        CustomName when !string.IsNullOrWhiteSpace(custom) => custom.Trim(),
        _ => "{SeriesName} - S{Season:00}E{Episode:00} - {EpisodeName}",
    };
}

public sealed record TemplateToken(string Value, string Label);
