using System.Text.RegularExpressions;
using Dapper;
using IIoT.Collector.Infrastructure;
using IIoT.Collector.Interfaces;
using IIoT.Collector.Repositories;
using IIoT.Collector.Services;
using IIoT.Collector.Workers;
using IIoT.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Serilog;

// Настройка и инициализация глобального логгера
SeriLogger.Configure();

try
{
    Log.Information("Starting IIoT.Collector...");

    DefaultTypeMap.MatchNamesWithUnderscores = true;
    SqlMapper.AddTypeHandler(new JsonTypeHandler<SensorUiConfig>());

    // Создание билдера хоста (Generic Host)
    var builder = Host.CreateApplicationBuilder(args);

    // NpgsqlDataSource с нативным маппингом enum-типов PostgreSQL
    var connStr =
        builder.Configuration.GetConnectionString("ADAMDB")
        ?? throw new InvalidOperationException("Connection string 'ADAMDB' not found.");
    var translator = new CollectorNameTranslator();
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connStr);
    dataSourceBuilder.MapEnum<SensorDataType>("sensor_data_type", translator);
    dataSourceBuilder.MapEnum<ModbusRegisterType>("modbus_register_type", translator);
    dataSourceBuilder.MapEnum<ServiceStatus>("system_service_status", translator);
    var dataSource = dataSourceBuilder.Build();
    builder.Services.AddSingleton(dataSource);

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
    builder.Services.AddSingleton<IProcessService, ProcessService>();
    // BufferRepository - локальный кэш (Singleton, так как SQLite файл один)
    builder.Services.AddSingleton<IBufferRepository, SqliteBufferRepository>();

    // 3. Регистрация основного фонового сервиса (Worker)
    builder.Services.AddHostedService<ModbusWorker>();

    var host = builder.Build();
    Log.Information("IIoT.Collector started.");

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
