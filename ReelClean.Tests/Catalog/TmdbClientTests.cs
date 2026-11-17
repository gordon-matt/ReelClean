using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReelClean.Services;
using ReelClean.Services.Catalog;

namespace ReelClean.Tests.Catalog;

public class TmdbClientTests
{
    [Fact]
    public async Task SearchSeriesAsync_MapsTheTvSearchPayload()
    {
        var client = Client("""
            {
              "results": [
                { "id": 2316, "name": "The Office", "first_air_date": "2005-03-24", "poster_path": "/office.jpg", "overview": "A mockumentary." }
              ]
            }
            """);

        var series = await client.SearchSeriesAsync("office", CancellationToken.None);

        var hit = Assert.Single(series);
        Assert.Equal(2316, hit.TmdbId);
        Assert.Equal("The Office", hit.Name);
        Assert.Equal(2005, hit.FirstAirYear);
        Assert.Equal("https://image.tmdb.org/t/p/w342/office.jpg", hit.PosterUrl);
        Assert.Equal("A mockumentary.", hit.Overview);
        Assert.Equal(new DateOnly(2005, 3, 24), hit.FirstAirDate);
    }

    [Fact]
    public async Task GetSeriesAsync_IncludesSeasonZero()
    {
        var client = Client("""
            {
              "id": 2316,
              "name": "The Office",
              "first_air_date": "2005-03-24",
              "seasons": [
                { "season_number": 0 },
                { "season_number": 1 }
              ]
            }
            """);

        var entry = await client.GetSeriesAsync(2316, CancellationToken.None);

        Assert.Equal([0, 1], entry.SeasonNumbers);
    }

    [Fact]
    public async Task GetSeasonAsync_MapsEpisodesAndSkipsABlankAirDate()
    {
        var client = Client("""
            {
              "episodes": [
                { "id": 10, "season_number": 1, "episode_number": 1, "name": "Pilot", "air_date": "2005-03-24" },
                { "id": 11, "season_number": 1, "episode_number": 2, "name": "Next", "air_date": "" }
              ]
            }
            """);

        var episodes = await client.GetSeasonAsync(2316, 1, CancellationToken.None);

        Assert.Equal(2, episodes.Count);
        Assert.Equal(new DateOnly(2005, 3, 24), episodes[0].AirDate);
        Assert.Null(episodes[1].AirDate);
    }

    [Fact]
    public async Task GetAsync_Unauthorized_DoesNotIncludeTheKeyInTheException()
    {
        var client = Client("{}", HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<TmdbRequestException>(() => client.SearchSeriesAsync("office", CancellationToken.None));

        Assert.Equal("TMDB rejected the API key.", ex.Message);
        Assert.DoesNotContain("secret-key", ex.Message, StringComparison.Ordinal);
    }

    private static TmdbClient Client(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var options = Options.Create(new ReelCleanOptions { Tmdb = new TmdbOptions { ApiKey = "secret-key" } });
        return new TmdbClient(http, options, NullLogger<TmdbClient>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Contains("api_key=secret-key", request.RequestUri?.Query, StringComparison.Ordinal);
            return Task.FromResult(respond(request));
        }
    }
}
