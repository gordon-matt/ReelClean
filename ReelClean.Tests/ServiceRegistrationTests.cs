using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReelClean.Services;
using ReelClean.Services.Catalog;
using ReelClean.Services.Parsing;
using ReelClean.Services.Renaming;

namespace ReelClean.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddReelCleanServices_ResolvesThePipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var options = new ReelCleanOptions();
        services.AddSingleton(options);
        services.AddSingleton<IOptions<ReelCleanOptions>>(_ => Options.Create(options));
        services.AddReelCleanServices();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FilenameParser>(provider.GetRequiredService<IFilenameParser>());
        Assert.IsType<TmdbMetadataProvider>(provider.GetRequiredService<ITvMetadataProvider>());
        Assert.IsType<RenameExecutor>(provider.GetRequiredService<IRenameExecutor>());
    }
}
