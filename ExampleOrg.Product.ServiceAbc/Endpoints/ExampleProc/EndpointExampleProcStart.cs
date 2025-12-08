using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.Endpoints.ExampleProc;

public partial class EndpointExampleProcStart
{
    [LwxEndpoint(
        Uri = "GET /example-proc/start"
    )]
    public static async Task Execute(HttpContext context)
    {
        var procId = Guid.NewGuid().ToString("N").Substring(0, 7); // Generate a simple proc-id
        await context.Response.WriteAsJsonAsync(new { message = "Process started", procId });
    }
}