using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModbusClient.Interfaces;
using ModbusClient.Repositories;
using ModbusClient.Services;
using ModbusClient.Workers;
using Serilog;

// Настройка и инициализация глобального логгера
SeriLogger.Configure();

try
{
    Log.Information("Starting ModbusClient...");

    // Создание билдера хоста (Generic Host)
    var builder = Host.CreateApplicationBuilder(args);

    // 1. Подключаем Serilog к инфраструктуре логирования .NET
    builder.Logging.ClearProviders();
    builder.Services.AddSerilog();

    // 2. Регистрация зависимостей (DI Container)

    // Репозитории (Scoped - создаются заново для каждого Scope/Запроса)
    builder.Services.AddScoped<IDataRepository, DataRepository>();

    // Доменные сервисы и инфраструктура (Singleton - живут всё время жизни приложения)
    // ModbusService - драйвер
    builder.Services.AddSingleton<IModbusService, ModbusService>();
    // DeviceService - управление соединениями (хранит state)
    builder.Services.AddSingleton<IDeviceService, DeviceService>();
    // ReadingService - бизнес-логика расчетов (Stateless)
    builder.Services.AddSingleton<IReadingService, ReadingService>();
    // BufferRepository - локальный кэш (Singleton, так как SQLite файл один)
    builder.Services.AddSingleton<IBufferRepository, SqliteBufferRepository>();

    // 3. Регистрация основного фонового сервиса (Worker)
    builder.Services.AddHostedService<ModbusWorker>();

    var host = builder.Build();
    Log.Information("ModbusClient started.");

    // Запуск хоста и ожидание завершения (например, SIGTERM)
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    // Гарантированный сброс логов перед выходом
    Log.CloseAndFlush();
}
