using IIoT.WebApi.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace IIoT.WebApi.Hubs;

/// <summary>
/// Канал диспетчеризации команд управления (Control Feedback / RPC).
/// Одностороннее вещание сервера о смене статуса команд. Отделён от телеметрии,
/// чтобы мобильные readonly-клиенты не получали служебный трафик управления.
/// </summary>
public class ControlHub : Hub<IControlClient>
{
    private readonly Serilog.ILogger _logger = Log.ForContext<ControlHub>();

    public override Task OnConnectedAsync()
    {
        _logger.Information("Control client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.Information("Control client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
