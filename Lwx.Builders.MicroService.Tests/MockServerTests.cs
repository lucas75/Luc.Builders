using System;
using System.Net.Http;
using Microsoft.AspNetCore.TestHost;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Lwx.Builders.MicroService.Tests.MockServices;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Lwx.Builders.MicroService.Tests;

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
        await using var runner = await MockServer.StartDevAsync();
        var runnerCounter = runner.Counters;
        runnerCounter.Reset();
        var client = runner.Client;        
        Assert.Equal("hello", await runner.GetWithTimeoutAsync("/stage-dev-only/hello"));        
        Assert.Equal("hello", await runner.GetWithTimeoutAsync("/stage-all/hello"));        
        Assert.Null(await runner.GetWithTimeoutAsync("/stage-none/hello"));
        await Task.Delay(1200);
        Assert.Equal(0, runnerCounter.WorkerStageNoneTicks);
        Assert.True(runnerCounter.WorkerStageNoneTicks == 0, "Expected a none worker heartbeat = 0 in Development mode");
        Assert.True(runnerCounter.WorkerStageDevelopmentOnlyTicks > 0, "Expected a dev (DevelopmentOnly) worker heartbeat > 0 in Development mode");
        Assert.True(runnerCounter.WorkerStageAllTicks > 0, "Expected an 'All' worker heartbeat > 0 in Development mode");
    }

    /// <summary>
    /// Starts a production server and ensures DevelopmentOnly endpoints are not exposed,
    /// while 'All' stage endpoints are available. Verifies worker heartbeats for only 'All' workers.
    /// </summary>
    [Fact(DisplayName = "Production server: omits DevelopmentOnly endpoints; All workers run")]
    public async Task RunPrdServer_Omits_DevEndpoint()
    {
        await using var runner = await MockServer.StartPrdAsync();
        var runnerCounter2 = runner.Counters;
        runnerCounter2.Reset();
        var client = runner.Client;        
        Assert.Equal("hello", await runner.GetWithTimeoutAsync("/stage-all/hello"));        
        Assert.Null(await runner.GetWithTimeoutAsync("/stage-dev-only/hello"));        
        Assert.Null(await runner.GetWithTimeoutAsync("/stage-none/hello"));
        await Task.Delay(1200);        
        Assert.Equal(0, runnerCounter2.WorkerStageNoneTicks);
        Assert.Equal(0, runnerCounter2.WorkerStageDevelopmentOnlyTicks);
        Assert.True(runnerCounter2.WorkerStageAllTicks > 0, "Expected 'All' worker heartbeat > 0 in Production mode");
    }
}
