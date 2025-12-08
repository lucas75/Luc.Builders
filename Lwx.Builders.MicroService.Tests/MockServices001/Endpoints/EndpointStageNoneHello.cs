using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.MockServices001.Endpoints;

public static partial class EndpointStageNoneHello
{
    [LwxEndpoint(
        Uri = "GET /stage-none/hello",
        Summary = "this endpoint is not published in any stage (is disabled)",
        Publish = LwxStage.None
    )]
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello";
    }
}
