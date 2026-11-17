using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReelClean.Services.Catalog;

/// <summary>
/// TMDB TV calls used for renaming. Series search is <c>/search/tv</c>,
/// season numbers come from <c>/tv/{id}</c>, and episodes from <c>/tv/{id}/season/{n}</c>.
/// </summary>
internal sealed class TmdbClient(HttpClient http, IOptions<ReelCleanOptions> options, ILogger<TmdbClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Tmdb.ApiKey);

    public async Task<IReadOnlyList<TvSeries>> SearchSeriesAsync(string query, CancellationToken cancellationToken)
    {
        var page = await GetAsync<TmdbPage>($"search/tv?query={Uri.EscapeDataString(query)}", cancellationToken);
        if (page is null)
        {
            return [];
        }

        return page.Results
            .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => ToSeries(item.Id, item.Name, item.FirstAirDate, item.PosterPath, item.Overview))
            .ToList();
    }

    public async Task<SeriesCacheEntry> GetSeriesAsync(int seriesId, CancellationToken cancellationToken)
    {
        var details = await GetAsync<TmdbSeriesDetails>($"tv/{seriesId.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
        if (details is null || details.Id <= 0 || string.IsNullOrWhiteSpace(details.Name))
        {
            throw new TmdbRequestException("That series was not found on TMDB.");
        }

        var seasons = details.Seasons
            .Select(season => season.SeasonNumber)
            .Distinct()
            .Order()
            .ToList();
        var series = ToSeries(details.Id, details.Name, details.FirstAirDate, details.PosterPath, details.Overview);
        return new SeriesCacheEntry(series, seasons);
    }

    public async Task<IReadOnlyList<TvEpisode>> GetSeasonAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken)
    {
        string path = $"tv/{seriesId.ToString(CultureInfo.InvariantCulture)}/season/{seasonNumber.ToString(CultureInfo.InvariantCulture)}";
        var season = await GetAsync<TmdbSeason>(path, cancellationToken);
        if (season is null)
        {
            return [];
        }

        return season.Episodes
            .Where(episode => episode.Id > 0)
            .Select(episode => new TvEpisode(
                episode.Id,
                seriesId,
                episode.SeasonNumber,
                episode.EpisodeNumber,
                string.IsNullOrWhiteSpace(episode.Name) ? $"Episode {episode.EpisodeNumber}" : episode.Name.Trim(),
                AirDate(episode.AirDate)))
            .OrderBy(episode => episode.EpisodeNumber)
            .ToList();
    }

    private async Task<T?> GetAsync<T>(string relative, CancellationToken cancellationToken)
    {
        string key = Uri.EscapeDataString(options.Value.Tmdb.ApiKey);
        string separator = relative.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        string request = $"{relative}{separator}api_key={key}&language=en-US";

        using var response = await http.GetAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("TMDB {Path} returned {StatusCode}", relative.Split('?')[0], (int)response.StatusCode);
            throw new TmdbRequestException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "TMDB rejected the API key.",
                HttpStatusCode.NotFound => "That series was not found on TMDB.",
                HttpStatusCode.TooManyRequests => "TMDB rate limit reached. Wait a moment and try again.",
                _ => "The TV catalog is unavailable right now.",
            });
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private static int? Year(string? date) =>
        DateOnly.TryParse(date, CultureInfo.InvariantCulture, out var parsed) ? parsed.Year : null;

    private static DateOnly? AirDate(string? date) =>
        DateOnly.TryParse(date, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static TvSeries ToSeries(int id, string? name, string? firstAirDate, string? posterPath, string? overview)
    {
        string? about = string.IsNullOrWhiteSpace(overview) ? null : overview.Trim();
        return new TvSeries(id, name!.Trim(), Year(firstAirDate), Poster(posterPath), about, AirDate(firstAirDate));
    }

    private static string? Poster(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : $"https://image.tmdb.org/t/p/w342{path}";

    private sealed class TmdbPage
    {
        public List<TmdbSeries> Results { get; set; } = [];
    }

    private sealed class TmdbSeries
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("first_air_date")]
        public string? FirstAirDate { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        public string? Overview { get; set; }
    }

    private sealed class TmdbSeriesDetails
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("first_air_date")]
        public string? FirstAirDate { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        public string? Overview { get; set; }

        public List<TmdbSeasonSummary> Seasons { get; set; } = [];
    }

    private sealed class TmdbSeasonSummary
    {
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }
    }

    private sealed class TmdbSeason
    {
        public List<TmdbEpisode> Episodes { get; set; } = [];
    }

    private sealed class TmdbEpisode
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("air_date")]
        public string? AirDate { get; set; }
    }
}
