using ReelClean.Services.Catalog;
using ReelClean.Services.Matching;
using ReelClean.Services.Naming;
using ReelClean.Services.Parsing;
using ReelClean.Services.Planning;
using ReelClean.Services.Renaming;
using ReelClean.Services.Scanning;
using ReelClean.Services.Settings;

namespace ReelClean.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReelCleanServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(TmdbClient), client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddSingleton<IFilenameParser, FilenameParser>();
        services.AddSingleton<IEpisodeMatcher, EpisodeMatcher>();
        services.AddSingleton<IFilenameSanitizer, FilenameSanitizer>();
        services.AddSingleton<IRenameTemplateFormatter, RenameTemplateFormatter>();
        services.AddSingleton<IRenamePlanner, RenamePlanner>();
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<IFileMover, SystemFileMover>();
        services.AddSingleton<IMetadataCache>(_ => new JsonMetadataCache(AppPaths.CacheDirectory));
        services.AddSingleton<ITvMetadataProvider, TmdbMetadataProvider>();
        services.AddSingleton<IRenameExecutor>(sp => new RenameExecutor(
            sp.GetRequiredService<IRenamePlanner>(),
            sp.GetRequiredService<IFileMover>(),
            sp.GetRequiredService<ILogger<RenameExecutor>>(),
            AppPaths.HistoryDirectory));
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(AppPaths.SettingsFile));

        return services;
    }
}
