using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Lwx.Builders.MicroService.Atributes;
using System;
using System.Linq;

namespace OkService;

[LwxService(PublishSwagger = LwxStage.None)]
public static partial class Service
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Reset counters at startup so runs are deterministic
        WorkerCounters.Reset();

        var workerEntries = builder.Services.Where(sd => sd.ServiceType?.FullName?.EndsWith("LwxWorkerDescriptor") == true)
            .Select(sd => sd.ImplementationInstance)
            .OfType<object>()
            .Select(impl => new
            {
                Name = impl?.GetType().GetProperty("Name")?.GetValue(impl)?.ToString() ?? "Unknown",
                Threads = impl?.GetType().GetProperty("Threads")?.GetValue(impl)
            })
            .ToArray();

        foreach (var w in workerEntries)
        {
            Console.WriteLine($"Worker: {w.Name} nThreads={w.Threads}");
        }

        // NOTE: The fallback shutdown has been moved to a background worker (StopWorker)
    }

    public static void Configure(WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var endpoints = ((IEndpointRouteBuilder)app).DataSources
                    .SelectMany(ds => ds.Endpoints)
                    .Where(e => e.Metadata.GetMetadata<LwxEndpointMetadata>() != null);

                foreach (var e in endpoints)
                {
                    var displayName = e.DisplayName ?? e.Metadata.GetMetadata<object>()?.GetType().Name ?? "Unnamed";
                    Console.WriteLine($"Endpoint: {displayName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Endpoint: inspection failed: {ex.Message}");
            }
        });
    }
}
