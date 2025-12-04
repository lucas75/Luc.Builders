// IMPORTANT: Do not change this file without asking the operator first.
// This class is critical for test infrastructure and any modifications could break tests or performance.

// IMPORTANT: Do not change this file without asking the operator first.
// This class is critical for test infrastructure and any modifications could break tests or performance.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace Lwx.Builders.MicroService.Tests.MockServices;


public sealed class MockServer : IAsyncDisposable
{
    public required IHost Host { get; init; }
    public required HttpClient Client { get; init; }
    public required string Environment { get; init; }
    public int RunningInstances;    
    
    private static MockServer? _devServer = null;
    private static MockServer? _prdServer = null;
    private static readonly SemaphoreSlim _lock = new(1, 1);
        
    public static async Task<MockServer> StartDevServer()
    {
        await _lock.WaitAsync();
        try
        {
            if (_devServer is not null)
            {
                _devServer.RunningInstances++;
                return _devServer;
            }
            // Create a new dev server instance
            {            
                _devServer = await StartAsync("Development");            
                _devServer.RunningInstances++;
                return _devServer;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    public static async Task<MockServer> StartPrdServer()
    {
        await _lock.WaitAsync();
        try
        {
            if (_prdServer is not null)
            {
                _prdServer.RunningInstances++;
                return _prdServer;
            }
            _prdServer = await StartAsync("Production");
            _prdServer.RunningInstances++;
            return _prdServer;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<MockServer> StartAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = environment;

        // Reduce noisy framework logging during tests (keeping warnings/errors).
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Load static JSON configuration for tests from MockServices/MockServer.appsettings.json
        var jsonPath = ResolveAppSettingsPath();
        builder.Configuration.AddJsonFile(jsonPath, optional: false, reloadOnChange: false);
        
        // Register per-host worker counters
        builder.Services.AddSingleton<IWorkerCounters, WorkerCounters>();

        // Configure both mock services - these calls reference generated code
        // and will cause a compile error if the generator doesn't produce them
        MockServices001.Service.Configure(builder);
        MockServices002.Service.Configure(builder);

        var app = builder.Build();
        // Configure both mock services app middleware
        MockServices001.Service.Configure(app);
        MockServices002.Service.Configure(app);
        await app.StartAsync();
        var client = app.GetTestClient();

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var tcs = new TaskCompletionSource();
        lifetime.ApplicationStarted.Register(() => tcs.SetResult());
        await tcs.Task;
                
        return new MockServer
        {
            Host = app,
            Client = client,
            RunningInstances = 0,
            Environment = environment,
        };
    }

    private static string ResolveAppSettingsPath()
    {
        // First look relative to runtime base directory, then walk up the file tree to find MockServices/MockServer.appsettings.json
        var baseDir = AppContext.BaseDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var candidate = Path.Combine(baseDir!, "MockServices", "MockServer.appsettings.json");
        if (File.Exists(candidate)) return candidate;

        var dir = new DirectoryInfo(baseDir!);
        while (dir != null)
        {
            candidate = Path.Combine(dir.FullName, "MockServices", "MockServer.appsettings.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not find MockServices/MockServer.appsettings.json in the output or ancestor directories.");
    }

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
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        bool dispose = false;
        await _lock.WaitAsync();
        try
        {
            RunningInstances--;
            if (RunningInstances <= 0)
            {
                dispose = true;
                if(Environment == "Development")
                {
                    _devServer = null;            
                }
                else if(Environment == "Production")
                {
                    _prdServer = null;                
                }                                
            }
        }
        finally 
        {
            _lock.Release();            
        }

        if(dispose)
        {
            try { Client.Dispose(); } catch { }
            try { await Host.StopAsync(); } catch { }  
        }        
    }
}
