using ExampleOrg.Product.ServiceAbc.Dto;
using Lwx.Builders.MicroService.Atributes;

namespace ExampleOrg.Product.ServiceAbc.Endpoints.Proc01;

[LwxEndpoint(
    Uri = "POST /proc01/start",
    SecurityProfile = "public",
    Summary = "Start proccess 01",
    Description = "Blah Blah",
    Publish = LwxStage.DevelopmentOnly
)]
public static partial class EndpointProc01Start
{
    public async static Task<SimpleResponseDto> Execute(

    )
    {
        // pretend is doing work
        await Task.CompletedTask;


        return new SimpleResponseDto { Ok = true };
    }
}