using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModbusClient.Interfaces;
using ModbusClient.Repositories;
using ModbusClient.Services;
using ModbusClient.Workers;
using Serilog;

SeriLogger.Configure();

try
{
    Log.Information("Starting Modbus Client...");

    var builder = Host.CreateApplicationBuilder(args);

    // 1. Подключаем Serilog к инфраструктуре Microsoft
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog();

    // 2. Регистрация сервисов
    builder.Services.AddScoped<IDataRepository, DataRepository>(); // Репозиторий для БД

    // Domain Services
    builder.Services.AddSingleton<IModbusService, ModbusService>();
    builder.Services.AddSingleton<IDeviceService, DeviceService>();
    builder.Services.AddSingleton<IReadingService, ReadingService>();

    // 3. Регистрация самого воркера (фоновая задача)
    builder.Services.AddHostedService<ModbusWorker>();

    var host = builder.Build();
    Log.Information("ModbusClient is running.");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
