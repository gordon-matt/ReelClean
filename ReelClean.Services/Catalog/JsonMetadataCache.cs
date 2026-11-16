using System.Text.Json;

namespace ReelClean.Services.Catalog;

public sealed class JsonMetadataCache(string root) : IMetadataCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public Task<Result<SeriesCacheEntry>> GetSeriesAsync(int seriesId, CancellationToken cancellationToken = default) =>
        ReadAsync<SeriesCacheEntry>(SeriesPath(seriesId), "That series is not cached.", cancellationToken);

    public async Task SaveSeriesAsync(SeriesCacheEntry entry, CancellationToken cancellationToken = default)
    {
        await WriteAsync(SeriesPath(entry.Series.TmdbId), entry, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<TvEpisode>>> GetSeasonAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        var cached = await ReadAsync<List<TvEpisode>>(SeasonPath(seriesId, seasonNumber), "That season is not cached.", cancellationToken);
        return cached.IsSuccess
            ? Result<IReadOnlyList<TvEpisode>>.Success(cached.Value)
            : Result<IReadOnlyList<TvEpisode>>.NotFound(cached.Errors.FirstOrDefault() ?? "That season is not cached.");
    }

    public Task SaveSeasonAsync(int seriesId, int seasonNumber, IReadOnlyList<TvEpisode> episodes, CancellationToken cancellationToken = default) =>
        WriteAsync(SeasonPath(seriesId, seasonNumber), episodes, cancellationToken);

    private async Task<Result<T>> ReadAsync<T>(string path, string missingMessage, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result<T>.NotFound(missingMessage);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, Json, cancellationToken);
            return value is null
                ? Result<T>.Error("Cached metadata could not be read.")
                : Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            return Result<T>.Error("Cached metadata could not be read. " + ex.Message);
        }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, Json, cancellationToken);
    }

    private string SeriesPath(int seriesId) => Path.Combine(root, seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture), "series.json");

    private string SeasonPath(int seriesId, int seasonNumber) =>
        Path.Combine(root, seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture), $"season-{seasonNumber:00}.json");
}
