namespace ReelClean.Services.Catalog;

public interface IMetadataCache
{
    Task<Result<SeriesCacheEntry>> GetSeriesAsync(int seriesId, CancellationToken cancellationToken = default);

    Task SaveSeriesAsync(SeriesCacheEntry entry, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TvEpisode>>> GetSeasonAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default);

    Task SaveSeasonAsync(int seriesId, int seasonNumber, IReadOnlyList<TvEpisode> episodes, CancellationToken cancellationToken = default);
}
