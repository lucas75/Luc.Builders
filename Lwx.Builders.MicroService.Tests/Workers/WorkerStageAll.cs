using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.Workers;

[LwxWorker(
    Stage = LwxStage.All,
    Threads = 1,
    Description = "This worker runs in all stages (dev, staging, production)"
)]
public partial class WorkerStageAll(ILogger<WorkerStageAll> logger, IWorkerCounters counters) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerStageAll starting up.");
        while (!stoppingToken.IsCancellationRequested)
        {
            counters.IncWorkerStageAllTicks();
            logger.LogInformation("WorkerStageAll heartbeat at {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        logger.LogInformation("WorkerStageAll stopping.");
    }
}
