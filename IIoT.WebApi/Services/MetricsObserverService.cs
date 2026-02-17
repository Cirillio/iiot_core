using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;
using IIoT.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Utilities;
using Npgsql;
using Serilog;

namespace IIoT.WebApi.Services
{
    public class MetricsObserverService(
        DapperContext context,
        IHubContext<MonitoringHub, IMonitoringClient> hubContext
    ) : BackgroundService
    {
        private readonly DapperContext _context = context;
        private readonly IHubContext<MonitoringHub, IMonitoringClient> _hubContext = hubContext;
        private readonly Serilog.ILogger _logger = Log.ForContext<MetricsObserverService>();

        private const int RETRY_DELAY = 5000;

        private const string CHANNEL_NAME = "metrics_realtime";

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var conn = await _context.DataSource.OpenConnectionAsync(
                        cancellationToken
                    );

                    conn.Notification += async (o, e) =>
                    {
                        _logger.Information("Received notification: {Payload}", e.Payload);
                        try
                        {
                            await _hubContext.Clients.All.ReceiveMetrics(e.Payload);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Broadcast failure | Time: " + DateTime.Now);
                        }
                    };

                    using var cmd = new NpgsqlCommand($"LISTEN {CHANNEL_NAME}", conn);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await conn.WaitAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Warning("MetricsObserverService is stopping due to cancellation.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in MetricsObserverService");
                    await Task.Delay(RETRY_DELAY, cancellationToken);
                }
            }
        }
    }
}
