using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReelClean.Services;
using ReelClean.Services.Settings;
using ReelClean.ViewModels;

namespace ReelClean;

public static class AppHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile(
            Path.Combine(builder.Environment.ContentRootPath, "appsettings.Local.json"),
            optional: true,
            reloadOnChange: false);

        var options = new ReelCleanOptions();
        builder.Configuration.Bind(options);
        // The saved key lives in the OS app-data folder, which is outside the git repo.
        new JsonSettingsStore(AppPaths.SettingsFile).Apply(options);

        builder.Services.AddHttpClient<PosterLoader>(client => client.Timeout = TimeSpan.FromSeconds(20));
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IOptions<ReelCleanOptions>>(_ => Options.Create(options));
        builder.Services.AddReelCleanServices();
        builder.Services.AddSingleton<AvaloniaFolderPicker>();
        builder.Services.AddSingleton<IFolderPicker>(sp => sp.GetRequiredService<AvaloniaFolderPicker>());
        builder.Services.AddSingleton<AvaloniaUserDialog>();
        builder.Services.AddSingleton<IUserDialog>(sp => sp.GetRequiredService<AvaloniaUserDialog>());
        builder.Services.AddSingleton<MainWindowViewModel>();
        return builder.Build();
    }
}
