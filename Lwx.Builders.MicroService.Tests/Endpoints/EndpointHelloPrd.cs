using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributes;

namespace Lwx.Builders.MicroService.Tests.Endpoints;

[LwxEndpoint(
    Uri = "GET /hello-prd", 
    Summary = "hello-prd", 
    Publish = LwxStage.All
)]
public static partial class EndpointHelloPrd
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello-prd";
    }
}
