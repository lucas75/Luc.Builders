using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributes;

namespace OkService.Endpoints;

[LwxEndpoint(
    Uri = "GET /hello-none", 
    Summary = "hello-none", 
    Publish = LwxStage.None
)]
public static partial class EndpointHelloNone
{
    public async static Task<string> Execute()
    {
        await Task.CompletedTask;
        return "hello-none";
    }
}
