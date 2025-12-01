using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributes;
using MicroService.Workers;
using Microsoft.Extensions.Hosting;

namespace MicroService.Endpoints;

[LwxEndpoint(
    Uri = "GET /worker-status", 
    Summary = "worker-status", 
    Publish = LwxStage.Production
)]
public static partial class EndpointWorkerStatus
{
    public async static Task<object> Execute
    (
        IHostApplicationLifetime lifetime
    )
    {
        var status = new { 
            none = WorkerCounters.None, 
            dev = WorkerCounters.Dev, 
            prd = WorkerCounters.Prd 
        };

        // wake the StopWorker so it doesn't force timeout
        StopWorker.StopServer();

        return status;
    }
}
