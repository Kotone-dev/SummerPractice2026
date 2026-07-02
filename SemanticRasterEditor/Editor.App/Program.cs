using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Editor.Services;
using System;

namespace Editor.App;

class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<FileService>();
    }
}
