using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;
using Lwx.Builders.MicroService.Tests.MockServices;

namespace Lwx.Builders.MicroService.Tests.MockServices001.Workers;

[LwxWorker(
    Stage = LwxStage.None,
    Threads = 1,
    Description = "This worker does not run in any stage (is disabled)"
)]
public partial class WorkerStageNone(ILogger<WorkerStageNone> logger, IWorkerCounters counters) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerStageNone starting up.");
        while (!stoppingToken.IsCancellationRequested)
        {
            counters.IncWorkerStageNoneTicks();
            logger.LogInformation("WorkerStageNone heartbeat at {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        logger.LogInformation("WorkerStageNone stopping.");
    }
}
