namespace ReelClean.Services.Catalog;

public interface ITvMetadataProvider
{
    bool IsConfigured { get; }

    Task<Result<IReadOnlyList<TvSeries>>> SearchSeriesAsync(string query, CancellationToken cancellationToken = default);

    Task<Result<SeriesCacheEntry>> GetSeriesAsync(int seriesId, bool refresh, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TvEpisode>>> GetSeasonEpisodesAsync(
        int seriesId,
        int seasonNumber,
        bool refresh,
        CancellationToken cancellationToken = default);
}
