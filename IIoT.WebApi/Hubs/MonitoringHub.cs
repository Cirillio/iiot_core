using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIoT.WebApi.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Core;

namespace IIoT.WebApi.Hubs
{
    public class MonitoringHub : Hub<IMonitoringClient>
    {
        private readonly Serilog.ILogger _logger = Log.ForContext<IMonitoringClient>();

        public async Task ReceiveMetrics(string json)
        {
            await Clients.All.ReceiveMetrics(json);
        }

        public override Task OnConnectedAsync()
        {
            _logger.Information(
                "Client connected: {ConnectionId} | Time: {Time}",
                Context.ConnectionId,
                DateTime.Now
            );
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.Information(
                "Client disconnected: {ConnectionId} | Time: {Time} | Exception: {Exception}",
                Context.ConnectionId,
                DateTime.Now,
                exception?.Message ?? "No exception"
            );
            return base.OnDisconnectedAsync(exception);
        }
    }
}
