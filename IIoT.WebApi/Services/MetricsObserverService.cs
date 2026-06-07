using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;
using IIoT.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Serilog;

namespace IIoT.WebApi.Services;

/// <summary>
/// Фоновая служба для прослушивания событий PostgreSQL (LISTEN/NOTIFY).
/// Пересылает поступающие в БД метрики всем подключенным веб-клиентам через SignalR.
/// </summary>
public class MetricsObserverService(
    DapperContext context,
    IHubContext<MonitoringHub, IMonitoringClient> hubContext
) : BackgroundService
{
    private readonly DapperContext _context = context;
    private readonly IHubContext<MonitoringHub, IMonitoringClient> _hubContext = hubContext;
    private readonly Serilog.ILogger _logger = Log.ForContext<MetricsObserverService>();

    private const int INITIAL_RETRY_DELAY_MS = 1000;
    private const int MAX_RETRY_DELAY_MS = 30000;
    private const string CHANNEL_NAME = "metrics_realtime";

    /// <summary>
    /// Основной цикл прослушивания канала БД с механизмом восстановления при сбоях.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.Information("MetricsObserverService starting...");

        int currentRetryDelay = INITIAL_RETRY_DELAY_MS;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.Information("Attempting to connect to DB for NOTIFY listening...");

                // Открываем выделенное соединение для LISTEN.
                // Используем OpenConnectionAsync напрямую из DataSource для контроля жизненного цикла.
                using var conn = await _context.DataSource.OpenConnectionAsync(cancellationToken);

                // Сбрасываем задержку после успешного подключения
                currentRetryDelay = INITIAL_RETRY_DELAY_MS;

                // Подписываемся на события уведомлений
                conn.Notification += async (o, e) =>
                {
                    _logger.Debug(
                        "DB Notification received on channel {Channel}. Payload length: {Len}",
                        e.Channel,
                        e.Payload.Length
                    );
                    try
                    {
                        await _hubContext.Clients.All.ReceiveMetrics(e.Payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to broadcast metric to SignalR clients");
                    }
                };

                using var cmd = new NpgsqlCommand($"LISTEN {CHANNEL_NAME}", conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                _logger.Information(
                    "Successfully subscribed to DB channel: {Channel}",
                    CHANNEL_NAME
                );

                // Цикл ожидания уведомлений. WaitAsync эффективно освобождает поток.
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Если соединение разорвется, WaitAsync выбросит исключение
                    await conn.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("MetricsObserverService is stopping due to service shutdown.");
                break;
            }
            catch (PostgresException ex)
            {
                _logger.Error(
                    "Postgres Error [State: {Code}]: {Message}",
                    ex.SqlState,
                    ex.MessageText
                );
                await WaitBeforeRetry();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database connection failed in MetricsObserverService");
                await WaitBeforeRetry();
            }
        }

        async Task WaitBeforeRetry()
        {
            _logger.Warning("Reconnecting in {Delay}ms...", currentRetryDelay);
            try
            {
                await Task.Delay(currentRetryDelay, cancellationToken);
                // Экспоненциальный рост задержки
                currentRetryDelay = Math.Min(currentRetryDelay * 2, MAX_RETRY_DELAY_MS);
            }
            catch (OperationCanceledException) { }
        }
    }
}
