using ExampleOrg.Product.ServiceAbc.Dto;
using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.Endpoints;

[LwxEndpoint(
    Uri = "GET /abc/cde",
    SecurityProfile = "public",
    Summary = "abc",
    Description = "Blah Blah",
    Publish = LwxStage.DevelopmentOnly
)]
public static partial class EndpointAbcCde
{
    public async static Task<SimpleResponseDto> Execute(

    )
    {
        // pretend is doing work
        await Task.CompletedTask;


        return new SimpleResponseDto { Ok = true };
    }
}