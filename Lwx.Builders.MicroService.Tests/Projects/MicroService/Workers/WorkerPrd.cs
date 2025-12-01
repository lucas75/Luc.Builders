using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributes;

namespace MicroService.Workers;

[LwxWorker(
    Stage = LwxStage.Production, 
    Threads = 1
)]
public partial class WorkerPrd(ILogger<WorkerPrd> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerPrd starting up.");
        while (!stoppingToken.IsCancellationRequested)
        {
            WorkerCounters.IncPrd();
            logger.LogInformation("WorkerPrd heartbeat at {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        logger.LogInformation("WorkerPrd stopping.");
    }
}
