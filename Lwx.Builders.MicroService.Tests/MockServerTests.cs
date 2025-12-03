using System;
using System.Net.Http;
using Microsoft.AspNetCore.TestHost;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Lwx.Builders.MicroService.Tests.MockServices;
using Microsoft.Extensions.DependencyInjection;

namespace Lwx.Builders.MicroService.Tests;

public class MockServerTests
{
    [Fact]
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

    [Fact]
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
