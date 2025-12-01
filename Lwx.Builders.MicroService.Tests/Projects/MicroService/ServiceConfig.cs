using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Lwx.Builders.MicroService.Atributes;
using System;
using System.Linq;

namespace MicroService;

[LwxServiceConfig(GenerateMain = true, PublishSwagger = LwxStage.None)]
public static partial class ServiceConfig
{

    public static void Configure(WebApplicationBuilder builder)
    {
        // Reset counters at startup so runs are deterministic
        WorkerCounters.Reset();

        var workerEntries = builder.Services.Where(sd => sd.ServiceType?.FullName?.EndsWith("LwxWorkerDescriptor") == true)
            .Select(sd => sd.ImplementationInstance?.GetType().GetProperty("Name")?.GetValue(sd.ImplementationInstance)?.ToString() ?? sd.ServiceType?.Name ?? "Unknown")
            .ToArray();

        Console.WriteLine($"LwxWorkerDescriptors: {string.Join(", ", workerEntries)}");

        // NOTE: The fallback shutdown has been moved to a background worker (StopWorker)
    }

    public static void Configure(WebApplication app)
    {
        // health and worker-status are provided as Lwx endpoints in the Endpoints/ folder

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var epNames = ((IEndpointRouteBuilder)app).DataSources
                    .SelectMany(ds => ds.Endpoints)
                    .Where(e => e.Metadata.GetMetadata<LwxEndpointMetadata>() != null)
                    .Select(e => e.DisplayName ?? e.Metadata.GetMetadata<object>()?.GetType().Name ?? "Unnamed")
                    .ToArray();

                Console.WriteLine($"LwxEndpoints: {string.Join(", ", epNames)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LwxEndpoints: inspection failed: {ex.Message}");
            }
        });
    }
}
