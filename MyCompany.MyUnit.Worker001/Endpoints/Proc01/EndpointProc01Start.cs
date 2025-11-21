using MyCompany.MyUnit.Worker001.Dto;
using Lwx.Archetype.MicroService.Atributes;

namespace MyCompany.MyUnit.Worker001.Endpoints;

[LwxEndpoint(
    Uri = "POST /proc01/start",
    SecurityProfile = "public",
    Summary = "Start proccess 01",
    Description = "Blah Blah",    
    Publish = LwxStage.Development
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