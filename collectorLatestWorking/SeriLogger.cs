using Microsoft.Extensions.Configuration;
using Serilog;

/// <summary>
/// Статический класс для настройки логгера Serilog.
/// </summary>
public static class SeriLogger
{
    /// <summary>
    /// Конфигурирует глобальный логгер Serilog.
    /// Читает настройки из appsettings.json и переменных окружения.
    /// Настраивает вывод в консоль с кастомным шаблоном.
    /// </summary>
    public static void Configure()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                optional: true
            )
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }
}
