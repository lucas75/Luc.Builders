using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.Endpoints;

[LwxEndpoint(
    Uri = "GET /stage-none/hello",
    Summary = "this endpoint is not published in any stage (is disabled)",
    Publish = LwxStage.None
)]
public static partial class EndpointStageNoneHello
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello";
    }
}
