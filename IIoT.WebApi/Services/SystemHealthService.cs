using System.Diagnostics;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;

namespace IIoT.WebApi.Services;

/// <summary>
/// Фоновый сервис для обновления статуса Web API в таблице system_status.
/// </summary>
public class SystemHealthService(
    IServiceScopeFactory scopeFactory,
    ILogger<SystemHealthService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("System Health Service (Gateway) started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISystemRepository>();

                await repository.UpdateSystemStatusAsync(new SystemStatus
                {
                    ServiceName = "WebGateway",
                    Status = ServiceStatus.Online,
                    UptimeSeconds = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
                    LastSync = DateTime.UtcNow,
                    LastError = ""
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update Gateway health status.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
