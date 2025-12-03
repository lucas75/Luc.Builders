using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.MockServices001.Endpoints;

[LwxEndpoint(
    Uri = "GET /stage-all/hello",
    Summary = "This endpoint is published in all stages (dev, staging, production)",
    Publish = LwxStage.All
)]
public static partial class EndpointStageAllHello
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello";
    }
}
