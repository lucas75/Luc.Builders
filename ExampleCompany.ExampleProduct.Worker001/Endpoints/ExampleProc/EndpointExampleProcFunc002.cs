using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Mvc;

namespace ExampleCompany.ExampleProduct.Worker001.Endpoints.ExampleProc;

[LwxEndpoint("GET /example-proc/func002")]
public partial class EndpointExampleProcFunc002
{
    public static async Task Execute(HttpContext context, [FromQuery] string procId)
    {
        // Simulate processing func002
        await context.Response.WriteAsJsonAsync(new { message = $"Executed func002 for proc {procId}" });
    }
}