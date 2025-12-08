using Lwx.Builders.MicroService.Atributtes;
using Microsoft.AspNetCore.Mvc;

namespace ExampleOrg.Product.ServiceAbc.Endpoints.ExampleProc;

public partial class EndpointExampleProcFinish
{
    [LwxEndpoint(
        Uri = "GET /example-proc/finish"
    )]
    public static async Task Execute(HttpContext context, [FromQuery] string procId)
    {
        // Simulate finishing the process
        await context.Response.WriteAsJsonAsync(new { message = $"Process {procId} finished" });
    }
}