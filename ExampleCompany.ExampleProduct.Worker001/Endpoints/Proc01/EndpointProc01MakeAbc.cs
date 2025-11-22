using ExampleCompany.ExampleProduct.Worker001.Dto;
using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Mvc;
using Lwx.Archetype.MicroService;

namespace ExampleCompany.ExampleProduct.Worker001.Endpoints.Proc01;

[LwxEndpoint(
    Uri = "POST /proc01/make-abc",
    SecurityProfile = "public",
    Summary = "Start proccess 01",
    Description = "Blah Blah",
    Publish = LwxStage.Development
)]
public static partial class EndpointProc01MakeAbc
{
  public async static Task<SimpleResponseDto> Execute(
    [FromQuery(Name = "proc")] string procId
  )
  {
    // pretend is doing work
    await Task.CompletedTask;


    return new SimpleResponseDto { Ok = true };
  }
}