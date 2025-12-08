using Lwx.Builders.MicroService.Atributtes;
using Microsoft.AspNetCore.Mvc;

namespace ExampleOrg.Product.ServiceAbc.Endpoints.ExampleProc;

public partial class EndpointExampleProcFunc001
{
    [LwxEndpoint(
        Uri = "GET /example-proc/func001"
    )]
    public static async Task Execute(HttpContext context, [FromQuery] string procId)
    {
        // Simulate processing func001
        await context.Response.WriteAsJsonAsync(new { message = $"Executed func001 for proc {procId}" });
    }
}