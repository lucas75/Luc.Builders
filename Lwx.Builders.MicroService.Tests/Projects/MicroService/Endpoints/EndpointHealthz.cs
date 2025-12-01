using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributes;

namespace MicroService.Endpoints;

[LwxEndpoint(
    Uri = "GET /healthz", 
    Summary = "healthz", 
    Publish = LwxStage.Production
)]
public static partial class EndpointHealthz
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "healthy";
    }
}
