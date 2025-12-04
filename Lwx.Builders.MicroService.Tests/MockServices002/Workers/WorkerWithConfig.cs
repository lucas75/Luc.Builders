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
    Description = "Worker that reads configuration via [LwxSetting]"
)]
public partial class WorkerWithConfig(ILogger<WorkerWithConfig> logger) : BackgroundService
{
    [LwxSetting("WorkerWithConfig:Abc")]
    public static partial string Abc { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerWithConfig starting. config Abc={abc}", Abc);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
