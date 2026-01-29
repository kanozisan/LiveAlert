using LiveAlert.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace LiveAlert;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        var configPath = Path.Combine(FileSystem.AppDataDirectory, "config.json");
        builder.Services.AddSingleton(new ConfigManager(configPath));
        builder.Services.AddSingleton(sp => new HttpClient());
        builder.Services.AddSingleton<ILiveDetector, YouTubeLiveDetector>();
        builder.Services.AddSingleton<AlertMonitor>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
