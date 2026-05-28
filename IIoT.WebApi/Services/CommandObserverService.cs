using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;
using IIoT.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Serilog;

namespace IIoT.WebApi.Services;

/// <summary>
/// Фоновая служба прослушивания канала смены статуса команд (LISTEN/NOTIFY).
/// Транслирует события жизненного цикла команд в канал диспетчеризации (ControlHub).
/// </summary>
public class CommandObserverService(
    DapperContext context,
    IHubContext<ControlHub, IControlClient> hubContext
) : BackgroundService
{
    private readonly DapperContext _context = context;
    private readonly IHubContext<ControlHub, IControlClient> _hubContext = hubContext;
    private readonly Serilog.ILogger _logger = Log.ForContext<CommandObserverService>();

    private const int INITIAL_RETRY_DELAY_MS = 1000;
    private const int MAX_RETRY_DELAY_MS = 30000;
    private const string CHANNEL_NAME = "command_status_changed";

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.Information("CommandObserverService starting...");

        int currentRetryDelay = INITIAL_RETRY_DELAY_MS;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var conn = await _context.DataSource.OpenConnectionAsync(cancellationToken);
                currentRetryDelay = INITIAL_RETRY_DELAY_MS;

                conn.Notification += async (o, e) =>
                {
                    _logger.Debug("Command status notification: {Payload}", e.Payload);
                    try
                    {
                        await _hubContext.Clients.All.CommandStatusChanged(e.Payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to broadcast command status to control clients");
                    }
                };

                using var cmd = new NpgsqlCommand($"LISTEN {CHANNEL_NAME}", conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                _logger.Information("Subscribed to DB channel: {Channel}", CHANNEL_NAME);

                while (!cancellationToken.IsCancellationRequested)
                {
                    await conn.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("CommandObserverService is stopping due to service shutdown.");
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Connection failed in CommandObserverService");
                _logger.Warning("Reconnecting in {Delay}ms...", currentRetryDelay);
                try
                {
                    await Task.Delay(currentRetryDelay, cancellationToken);
                    currentRetryDelay = Math.Min(currentRetryDelay * 2, MAX_RETRY_DELAY_MS);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
