using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReelClean.ViewModels;
using ReelClean.Views;

namespace ReelClean;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var host = AppHost.Build(Environment.GetCommandLineArgs());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = host.Services.GetRequiredService<MainWindowViewModel>(),
            };
            host.Services.GetRequiredService<AvaloniaFolderPicker>().Owner = window;
            host.Services.GetRequiredService<AvaloniaUserDialog>().Owner = window;
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => host.Dispose();
        }
        else
        {
            host.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
