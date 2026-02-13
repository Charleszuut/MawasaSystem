using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MawasaProject.Application;
using MawasaProject.Infrastructure;

namespace MawasaProject.Presentation;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mawasa.db3");

        builder.Services
            .AddApplicationServices()
            .AddInfrastructureServices(dbPath)
            .AddPresentationServices();

        return builder.Build();
    }
}
