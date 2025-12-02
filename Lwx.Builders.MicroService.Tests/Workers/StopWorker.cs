using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributes;

namespace Lwx.Builders.MicroService.Tests.Workers;

[LwxWorker(
    Stage = LwxStage.All, 
    Threads = 1
)]
public partial class StopWorker
(
    ILogger<StopWorker> logger, 
    IHostApplicationLifetime lifetime
) : BackgroundService
{    
    static readonly TaskCompletionSource<bool> theLock = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static void StopServer()
    {
        theLock.TrySetResult(true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation($"started.");

            // Wait for either the lock Task to complete (someone called ReleaseLock) or a 5s timeout/cancellation.
            var waitTask = Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            var winner = await Task.WhenAny(theLock.Task, waitTask).ConfigureAwait(false);

        if (stoppingToken.IsCancellationRequested) 
        {
            logger.LogInformation("stopping due to cancellation.");                        
        }
            if (winner == theLock.Task)
        {
            logger.LogInformation("stopping due to signal.");                                    
        }
        else
        {
            logger.LogInformation("stopping due to timeout.");                                    
        }

        await Task.Delay(100, stoppingToken);
        
        logger.LogInformation("commanding shutdown...");
        lifetime.StopApplication();       
        logger.LogInformation("commanding shutdown... ok");
    }
}
