using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.MockServices002.Workers;

[LwxWorker(
    Stage = LwxStage.All,
    Threads = 1,
    Description = "Simple worker for MockServices002"
)]
public partial class WorkerSimple(ILogger<WorkerSimple> logger) : BackgroundService
{
    [LwxSetting("WorkerSimple:DefConfig")]
    public static partial string DefConfig { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerSimple starting up (config DefConfig = {config}).", DefConfig);
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("WorkerSimple heartbeat at {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        logger.LogInformation("WorkerSimple stopping.");
    }
}
