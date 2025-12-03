using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.Workers;

[LwxWorker(
    Stage = LwxStage.DevelopmentOnly,
    Threads = 1,
    Description = "This worker runs only in development stage"
)]
public partial class WorkerStageDevelopmentOnly(ILogger<WorkerStageDevelopmentOnly> logger, IWorkerCounters counters) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerStageDevelopmentOnly starting up.");
        while (!stoppingToken.IsCancellationRequested)
        {
            counters.IncWorkerStageDevelopmentOnlyTicks();
            logger.LogInformation("WorkerStageDevelopmentOnly heartbeat at {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        logger.LogInformation("WorkerStageDevelopmentOnly stopping.");
    }
}
