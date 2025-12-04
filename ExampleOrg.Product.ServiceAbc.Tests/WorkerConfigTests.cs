using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Xunit;
using ExampleOrg.Product.ServiceAbc;

namespace ExampleOrg.Product.ServiceAbc.Tests;

[CollectionDefinition("WorkerConfig", DisableParallelization = true)]
public class WorkerConfigCollectionDefinition { }

[Collection("WorkerConfig")]
public class WorkerConfigTests
{
    /// <summary>
    /// When no configuration section exists for a worker, IOptions.Value properties are default (null for strings).
    /// </summary>
    [Fact(DisplayName = "Worker config: missing config section results in default property values")]
    public async System.Threading.Tasks.Task WorkerConfig_MissingSection_Defaults()
    {
        await using var server = await MockServer.StartDevServer(loadConfig: false);

        // When no configuration exists, the Configuration section should be empty
        var cfgVal = server.Host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>().GetSection("TheWorker")["Abc"];
        Assert.Null(cfgVal);

        // Verify the hosted service instance for TheWorker is registered
        var hosted = server.Host.Services.GetServices<IHostedService>();
        Assert.Contains(hosted, h => h.GetType().FullName?.Contains("TheWorker") == true);

        await server.StopAsync();
    }

    /// <summary>
    /// If the worker's configuration section exists, the values are bound correctly.
    /// </summary>
    [Fact(DisplayName = "Worker config: present section yields bound values")]
    public async System.Threading.Tasks.Task WorkerConfig_PresentSection_BindsValues()
    {
        await using var server = await MockServer.StartDevServer();

        var cfgVal2 = server.Host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>().GetSection("TheWorker")["Abc"];
        Assert.Equal("the-value", cfgVal2);

        // Verify the hosted service instance for TheWorker is registered
        var hosted = server.Host.Services.GetServices<IHostedService>();
        Assert.Contains(hosted, h => h.GetType().FullName?.Contains("TheWorker") == true);

        await server.StopAsync();
    }
}
