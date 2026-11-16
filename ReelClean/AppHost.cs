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
        var options = new ReelCleanOptions();
        builder.Configuration.Bind(options);
        new JsonSettingsStore(AppPaths.SettingsFile).Apply(options);

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
