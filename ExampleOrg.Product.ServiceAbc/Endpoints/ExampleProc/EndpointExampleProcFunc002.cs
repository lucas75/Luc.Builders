using Lwx.Builders.MicroService.Atributtes;
using Microsoft.AspNetCore.Mvc;

namespace ExampleOrg.Product.ServiceAbc.Endpoints.ExampleProc;

public partial class EndpointExampleProcFunc002
{
    [LwxEndpoint(
        Uri = "GET /example-proc/func002"
    )]
    public static async Task Execute(HttpContext context, [FromQuery] string procId)
    {
        // Simulate processing func002
        await context.Response.WriteAsJsonAsync(new { message = $"Executed func002 for proc {procId}" });
    }
}