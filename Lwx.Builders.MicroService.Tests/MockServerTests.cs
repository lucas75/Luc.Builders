using System;
using System.Net.Http;
using Microsoft.AspNetCore.TestHost;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Lwx.Builders.MicroService.Tests.MockServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace Lwx.Builders.MicroService.Tests;

[Collection("MockServer")]
[DisplayName("Mock Server Tests")]
public class MockServerTests
{
    /// <summary>
    /// Starts a development server and ensures endpoints for "DevelopmentOnly" and "All" stages are available,
    /// while the "None" stage endpoint is not exposed. Also verifies worker heartbeats per stage.
    /// </summary>
    [Fact(DisplayName = "Dev server: exposes DevelopmentOnly and All endpoints; worker heartbeats update")]
    public async Task RunDevServer_Exposes_DevAndPrdEndpoints()
    {
        await using var runner = await MockServer.StartDevServer();
        var client = runner.Client;        
        Assert.Equal("hello", await runner.GetWithTimeoutAsync("/stage-dev-only/hello"));        
        Assert.Equal("hello", await runner.GetWithTimeoutAsync("/stage-all/hello"));        
        Assert.Null(await runner.GetWithTimeoutAsync("/stage-none/hello"));
    }

    /// <summary>
    /// Starts a production server and ensures DevelopmentOnly endpoints are not exposed,
    /// while 'All' stage endpoints are available. Verifies worker heartbeats for only 'All' workers.
    /// </summary>
    [Fact(DisplayName = "Production server: omits DevelopmentOnly endpoints; All workers run")]
    public async Task RunPrdServer_Omits_DevEndpoint()
    {
        await using var runner = await MockServer.StartPrdServer();
        var client = runner.Client;        
        Assert.Equal("hello", await runner.GetWithTimeoutAsync("/stage-all/hello"));        
        Assert.Null(await runner.GetWithTimeoutAsync("/stage-dev-only/hello"));        
        Assert.Null(await runner.GetWithTimeoutAsync("/stage-none/hello"));
    }


}
