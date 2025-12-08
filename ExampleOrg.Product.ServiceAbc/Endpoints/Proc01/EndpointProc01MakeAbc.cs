using ExampleOrg.Product.ServiceAbc.Dto;
using Lwx.Builders.MicroService.Atributtes;
using Microsoft.AspNetCore.Mvc;
// using Lwx.Builders.MicroService; (not required here)

namespace ExampleOrg.Product.ServiceAbc.Endpoints.Proc01;

public static partial class EndpointProc01MakeAbc
{
    [LwxEndpoint(
        Uri = "POST /proc01/make-abc",
        SecurityProfile = "public",
        Summary = "Start proccess 01",
        Description = "Blah Blah",
        Publish = LwxStage.DevelopmentOnly
    )]
    public async static Task<SimpleResponseDto> Execute(
      [FromQuery(Name = "proc")] string procId
    )
    {
        // pretend is doing work
        await Task.CompletedTask;


        return new SimpleResponseDto { Ok = true };
    }
}