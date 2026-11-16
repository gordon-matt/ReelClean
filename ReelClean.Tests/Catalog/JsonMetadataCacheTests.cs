using ReelClean.Services.Catalog;

namespace ReelClean.Tests.Catalog;

public class JsonMetadataCacheTests
{
    [Fact]
    public async Task SaveSeasonAsync_RoundTripsEpisodes()
    {
        using var folder = new TempFolder();
        var cache = new JsonMetadataCache(folder.Path);
        var episodes = new List<TvEpisode> { new(10, 7, 1, 1, "Pilot", new DateOnly(2005, 3, 24)) };

        await cache.SaveSeasonAsync(7, 1, episodes, CancellationToken.None);
        var loaded = await cache.GetSeasonAsync(7, 1, CancellationToken.None);

        Assert.True(loaded.IsSuccess);
        Assert.Equal("Pilot", Assert.Single(loaded.Value).Name);
        Assert.Equal(ResultStatus.NotFound, (await cache.GetSeasonAsync(7, 2, CancellationToken.None)).Status);
    }
}
