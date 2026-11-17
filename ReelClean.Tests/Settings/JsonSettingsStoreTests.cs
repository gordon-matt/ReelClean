using ReelClean.Services;
using ReelClean.Services.Settings;

namespace ReelClean.Tests.Settings;

public class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAsync_RoundTripsTheApiKeyWithoutPuttingItInSource()
    {
        using var folder = new TempFolder();
        string path = Path.Combine(folder.Path, "settings.json");
        var store = new JsonSettingsStore(path);
        var options = new ReelCleanOptions
        {
            Tmdb = new TmdbOptions { ApiKey = "secret-key" },
            ScanSubdirectories = true,
            AlwaysAutoMap = true,
            TemplateName = "Kodi",
        };

        await store.SaveAsync(options, CancellationToken.None);
        var loaded = new ReelCleanOptions();
        store.Apply(loaded);

        Assert.Equal("secret-key", loaded.Tmdb.ApiKey);
        Assert.True(loaded.ScanSubdirectories);
        Assert.True(loaded.AlwaysAutoMap);
        Assert.Equal("Kodi", loaded.TemplateName);
    }
}
