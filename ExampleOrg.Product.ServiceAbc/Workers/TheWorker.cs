using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributes;

namespace ExampleOrg.Product.ServiceAbc.Workers;

[LwxWorker(
    Name = "TheWorker",
    Description = "Example background worker for the sample application",
    Threads = 2,
    Policy = LwxWorkerPolicy.AlwaysHealthy,
    Stage = LwxStage.DevelopmentOnly
)]
public partial class TheWorker(ILogger<TheWorker> logger, [FromConfig("Abc")] string abc) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TheWorker starting up (config Abc = {abc}).", abc);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("TheWorker heartbeat at {time}", DateTimeOffset.UtcNow);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // graceful shutdown
                break;
            }
        }

        logger.LogInformation("TheWorker stopping.");
    }
}
