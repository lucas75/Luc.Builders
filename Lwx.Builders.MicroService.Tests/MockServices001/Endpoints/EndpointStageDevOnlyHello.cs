using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.MockServices001.Endpoints;

public static partial class EndpointStageDevOnlyHello
{
    [LwxEndpoint(
        Uri = "GET /stage-dev-only/hello",
        Summary = "this endpoint is published only in development stage",
        Publish = LwxStage.DevelopmentOnly
    )]
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello";
    }
}
