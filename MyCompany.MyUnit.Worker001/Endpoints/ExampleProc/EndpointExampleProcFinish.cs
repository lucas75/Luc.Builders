using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Mvc;

namespace MyCompany.MyUnit.Worker001.Endpoints.ExampleProc;

[LwxEndpoint("GET /example-proc/finish")]
public partial class EndpointExampleProcFinish
{
    public static async Task Execute(HttpContext context, [FromQuery] string procId)
    {
        // Simulate finishing the process
        await context.Response.WriteAsJsonAsync(new { message = $"Process {procId} finished" });
    }
}