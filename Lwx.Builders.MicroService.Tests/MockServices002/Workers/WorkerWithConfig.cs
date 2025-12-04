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
    Description = "Worker that reads configuration via [FromConfig]"
)]
public partial class WorkerWithConfig(ILogger<WorkerWithConfig> logger, [FromConfig("Abc")] string abc) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerWithConfig starting. config Abc={abc}", abc);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
