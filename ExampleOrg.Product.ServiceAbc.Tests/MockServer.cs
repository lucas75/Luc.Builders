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

namespace ExampleOrg.Product.ServiceAbc.Tests;

public sealed class MockServer : IAsyncDisposable
{
    public required IHost Host { get; init; }
    public required HttpClient Client { get; init; }
    public required string Environment { get; init; }
    public int RunningInstances;

    private static MockServer? _devServer = null;
    private static MockServer? _prdServer = null;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<MockServer> StartDevServer(bool loadConfig = true)
    {
        await _lock.WaitAsync();
        try
        {
            if (_devServer is not null)
            {
                _devServer.RunningInstances++;
                return _devServer;
            }
            _devServer = await StartAsync("Development", loadConfig);
            _devServer.RunningInstances++;
            return _devServer;
        }
        finally
        {
            _lock.Release();
        }
    }

    public static async Task<MockServer> StartPrdServer(bool loadConfig = true)
    {
        await _lock.WaitAsync();
        try
        {
            if (_prdServer is not null)
            {
                _prdServer.RunningInstances++;
                return _prdServer;
            }
            _prdServer = await StartAsync("Production", loadConfig);
            _prdServer.RunningInstances++;
            return _prdServer;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<MockServer> StartAsync(string environment, bool loadConfig = true)
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = environment;

        // Reduce noisy framework logging during tests (keeping warnings/errors).
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // If we don't want to load test configuration, clear all default configuration sources
        // so tests start with an empty configuration set. This avoids loading the project's
        // appsettings.json that would otherwise provide default values and influence tests.
        if (!loadConfig)
        {
            builder.Configuration.Sources.Clear();
        }
        else
        {
            // Load static JSON configuration for tests from MockServer.appsettings.json
            var jsonPath = ResolveAppSettingsPath();
            builder.Configuration.AddJsonFile(jsonPath, optional: false, reloadOnChange: false);
        }

        // Configure the service - this references generated code
        ExampleOrg.Product.ServiceAbc.Service.Configure(builder);

        var app = builder.Build();
        // Configure the service app middleware
        ExampleOrg.Product.ServiceAbc.Service.Configure(app);
        await app.StartAsync();
        var client = app.GetTestClient();

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
        // First look relative to runtime base directory, then walk up the file tree to find MockServer.appsettings.json
        var baseDir = AppContext.BaseDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var candidate = Path.Combine(baseDir!, "MockServer.appsettings.json");
        if (File.Exists(candidate)) return candidate;

        var dir = new DirectoryInfo(baseDir!);
        while (dir != null)
        {
            candidate = Path.Combine(dir.FullName, "MockServer.appsettings.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not find MockServer.appsettings.json in the output or ancestor directories.");
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
                if (Environment == "Development")
                {
                    _devServer = null;
                }
                else if (Environment == "Production")
                {
                    _prdServer = null;
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        if (dispose)
        {
            try { Client.Dispose(); } catch { }
            try { await Host.StopAsync(); } catch { }
        }
    }
}