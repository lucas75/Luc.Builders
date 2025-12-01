using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributes;

namespace MicroService.Workers;

[LwxWorker(    
    Stage = LwxStage.Development, 
    Threads = 1
)]
public partial class WorkerDev(ILogger<WorkerDev> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerDev starting up.");
        while (!stoppingToken.IsCancellationRequested)
        {
            WorkerCounters.IncDev();
            logger.LogInformation("WorkerDev heartbeat at {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        logger.LogInformation("WorkerDev stopping.");
    }
}
