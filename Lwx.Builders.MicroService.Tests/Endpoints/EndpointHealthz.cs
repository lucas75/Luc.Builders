using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributes;

namespace Lwx.Builders.MicroService.Tests.Endpoints;

[LwxEndpoint(
    Uri = "GET /healthz", 
    Summary = "healthz", 
    Publish = LwxStage.All
)]
public static partial class EndpointHealthz
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "healthy";
    }
}
