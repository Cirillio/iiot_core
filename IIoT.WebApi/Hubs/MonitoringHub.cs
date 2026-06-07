using IIoT.WebApi.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace IIoT.WebApi.Hubs;

/// <summary>
/// Хаб SignalR для трансляции телеметрии и системных уведомлений в реальном времени.
/// </summary>
public class MonitoringHub : Hub<IMonitoringClient>
{
    private readonly Serilog.ILogger _logger = Log.ForContext<MonitoringHub>();

    public override Task OnConnectedAsync()
    {
        _logger.Information("Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.Information(
            "Client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message ?? "None"
        );
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Трансляция метрик всем подключенным клиентам.
    /// </summary>
    public async Task ReceiveMetrics(string json) => await Clients.All.ReceiveMetrics(json);

    /// <summary>
    /// Уведомление об изменении конфигурации.
    /// </summary>
    public async Task NotifyConfigUpdated(string entityType, int entityId) =>
        await Clients.All.ConfigUpdated(entityType, entityId);

    /// <summary>
    /// Отправка системного уведомления/алерта.
    /// </summary>
    public async Task SendAlert(string message, string level) =>
        await Clients.All.SystemAlert(message, level);
}
