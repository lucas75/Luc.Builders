using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Mvc;

namespace ExampleCompany.ExampleProduct.Worker001.Endpoints.ExampleProc;

[LwxEndpoint("GET /example-proc/func001")]
public partial class EndpointExampleProcFunc001
{
    public static async Task Execute(HttpContext context, [FromQuery] string procId)
    {
        // Simulate processing func001
        await context.Response.WriteAsJsonAsync(new { message = $"Executed func001 for proc {procId}" });
    }
}