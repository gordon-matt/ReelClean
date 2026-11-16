namespace ReelClean.Services.Catalog;

public sealed class TmdbMetadataProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ReelCleanOptions> options,
    IMetadataCache cache,
    ILoggerFactory loggerFactory) : ITvMetadataProvider
{
    private readonly TmdbClient _client = new(
        httpClientFactory.CreateClient(nameof(TmdbClient)),
        options,
        loggerFactory.CreateLogger<TmdbClient>());

    private readonly ILogger _logger = loggerFactory.CreateLogger<TmdbMetadataProvider>();

    public bool IsConfigured => _client.IsConfigured;

    public Task<Result<IReadOnlyList<TvSeries>>> SearchSeriesAsync(string query, CancellationToken cancellationToken = default)
    {
        string term = query?.Trim() ?? string.Empty;
        if (term.Length < 2)
        {
            return Task.FromResult(Result<IReadOnlyList<TvSeries>>.Invalid(new ValidationError(nameof(query), "Enter at least 2 characters.")));
        }

        return CallAsync(SearchAsync(term, cancellationToken), cancellationToken);
    }

    public async Task<Result<SeriesCacheEntry>> GetSeriesAsync(int seriesId, bool refresh, CancellationToken cancellationToken = default)
    {
        if (seriesId <= 0)
        {
            return Result<SeriesCacheEntry>.Invalid(new ValidationError(nameof(seriesId), "That series id is not valid."));
        }

        if (!refresh)
        {
            var cached = await cache.GetSeriesAsync(seriesId, cancellationToken);
            if (cached.IsSuccess)
            {
                return cached;
            }
        }

        return await CallAsync(LoadSeriesAsync(seriesId, cancellationToken), cancellationToken);
    }

    public async Task<Result<IReadOnlyList<TvEpisode>>> GetSeasonEpisodesAsync(
        int seriesId,
        int seasonNumber,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        if (!refresh)
        {
            var cached = await cache.GetSeasonAsync(seriesId, seasonNumber, cancellationToken);
            if (cached.IsSuccess)
            {
                return cached;
            }
        }

        return await CallAsync(LoadSeasonAsync(seriesId, seasonNumber, cancellationToken), cancellationToken);
    }

    private async Task<Result<IReadOnlyList<TvSeries>>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!_client.IsConfigured)
        {
            return Result<IReadOnlyList<TvSeries>>.Error("TV search needs a TMDB API key. Save it in settings, or set Tmdb:ApiKey.");
        }

        var hits = await _client.SearchSeriesAsync(query, cancellationToken);
        return Result<IReadOnlyList<TvSeries>>.Success(hits);
    }

    private async Task<Result<SeriesCacheEntry>> LoadSeriesAsync(int seriesId, CancellationToken cancellationToken)
    {
        if (!_client.IsConfigured)
        {
            return Result<SeriesCacheEntry>.Error("TV search needs a TMDB API key. Save it in settings, or set Tmdb:ApiKey.");
        }

        var entry = await _client.GetSeriesAsync(seriesId, cancellationToken);
        await cache.SaveSeriesAsync(entry, cancellationToken);
        return Result<SeriesCacheEntry>.Success(entry);
    }

    private async Task<Result<IReadOnlyList<TvEpisode>>> LoadSeasonAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken)
    {
        if (!_client.IsConfigured)
        {
            return Result<IReadOnlyList<TvEpisode>>.Error("TV search needs a TMDB API key. Save it in settings, or set Tmdb:ApiKey.");
        }

        var episodes = await _client.GetSeasonAsync(seriesId, seasonNumber, cancellationToken);
        await cache.SaveSeasonAsync(seriesId, seasonNumber, episodes, cancellationToken);
        return Result<IReadOnlyList<TvEpisode>>.Success(episodes);
    }

    private async Task<Result<T>> CallAsync<T>(Task<Result<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            return await work;
        }
        catch (TmdbRequestException ex)
        {
            return Result<T>.Error(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "TMDB request failed");
            return Result<T>.Error("The TV catalog is unavailable right now.");
        }
    }
}
