using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributtes;

namespace Lwx.Builders.MicroService.Tests.MockServices002.Endpoints;

[LwxEndpoint(
    Uri = "GET /mock002/simple",
    Summary = "Simple endpoint for MockServices002",
    Publish = LwxStage.All
)]
public static partial class EndpointMock002Simple
{
    public static async Task<string> Execute()
    {
        await Task.CompletedTask;
        return "Hello from MockServices002";
    }
}
