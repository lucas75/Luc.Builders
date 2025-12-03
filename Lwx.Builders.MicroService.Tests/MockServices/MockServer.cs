using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Lwx.Builders.MicroService.Tests.MockServices;

public sealed class MockServer : IAsyncDisposable
{
    public IHost Host { get; }
    public HttpClient Client { get; }
        
        /// <summary>
        /// Convenience accessor to the per-host IWorkerCounters instance registered in the test host.
        /// Tests can use this to inspect or reset counters without reaching into Host.Services.
        /// </summary>
        public IWorkerCounters Counters => Host.Services.GetRequiredService<IWorkerCounters>();

    private MockServer(IHost host, HttpClient client)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public static async Task<MockServer> StartAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = environment;
        // Register per-host worker counters
        builder.Services.AddSingleton<IWorkerCounters, WorkerCounters>();
        Lwx.Builders.MicroService.Tests.Service.Configure(builder);
        var app = builder.Build();
        Lwx.Builders.MicroService.Tests.Service.Configure(app);
        await app.StartAsync();
        var client = app.GetTestClient();
        return new MockServer(app, client);
    }

    public static async Task<MockServer> StartDevAsync()
    {
        return await StartAsync("Development");
    }

    public static async Task<MockServer> StartPrdAsync()
    {
        return await StartAsync("Production");    
    }

    /// <summary>
    /// Perform a GET request against the test host with a short timeout (500ms) to fail fast in tests.
    /// Returns the response content if successful, or null for not-found/timeout.
    /// </summary>
    public async Task<string?> GetWithTimeoutAsync(string path, int timeoutMs = 500)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        try
        {
            var res = await Client.GetAsync(path, cts.Token);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadAsStringAsync();
            }
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await Host.StopAsync();
        }
        finally
        {
            Client.Dispose();
            Host.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
