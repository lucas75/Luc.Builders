using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributes;

namespace Lwx.Builders.MicroService.Tests.Endpoints;

[LwxEndpoint(
    Uri = "GET /hello-dev", 
    Summary = "hello-dev", 
    Publish = LwxStage.DevelopmentOnly
)]
public static partial class EndpointHelloDev
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello-dev";
    }
}
