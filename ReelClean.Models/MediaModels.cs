namespace ReelClean.Models;

public sealed record VideoFile(string FullPath, string FileName, string Extension, long FileSize);

public sealed record TvSeries(int TmdbId, string Name, int? FirstAirYear, string? PosterUrl);

public sealed record TvEpisode(
    int TmdbId,
    int SeriesId,
    int SeasonNumber,
    int EpisodeNumber,
    string Name,
    DateOnly? AirDate);

/// <summary>
/// What a filename parser could read. This is not a metadata match:
/// <see cref="Pattern"/> says which rule fired, and the matcher decides confidence later.
/// </summary>
public sealed record ParsedFilename(
    string OriginalFileName,
    int? SeasonNumber,
    IReadOnlyList<int> EpisodeNumbers,
    string? DetectedEpisodeTitle,
    string Pattern,
    string Diagnostic);

public sealed record EpisodeMatch(
    VideoFile File,
    ParsedFilename Parsed,
    IReadOnlyList<TvEpisode> Episodes,
    MatchConfidence Confidence,
    MatchStatus Status,
    string Explanation);
