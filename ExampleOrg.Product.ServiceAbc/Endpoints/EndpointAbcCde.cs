using ExampleOrg.Product.ServiceAbc.Dto;
using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.Endpoints;

public static partial class EndpointAbcCde
{
    [LwxEndpoint(
        Uri = "GET /abc/cde",
        SecurityProfile = "public",
        Summary = "abc",
        Description = "Blah Blah",
        Publish = LwxStage.DevelopmentOnly
    )]
    public async static Task<SimpleResponseDto> Execute(

    )
    {
        // pretend is doing work
        await Task.CompletedTask;


        return new SimpleResponseDto { Ok = true };
    }
}