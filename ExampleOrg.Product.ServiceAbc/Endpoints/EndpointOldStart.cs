using Lwx.MicroService.Atributes;
using Microsoft.AspNetCore.Http;

namespace ExampleOrg.Product.ServiceAbc.Endpoints;

[LwxEndpoint("GET /mismatch/start", NamingExceptionJustification = "Legacy route - keep this name for backward compatibility")]
public static partial class EndpointOldStart
{
    public static async Task Execute(HttpContext context)
    {
        await context.Response.WriteAsJsonAsync(new { ok = true, msg = "Legacy endpoint allowed by NamingExceptionJustification" });
    }
}
