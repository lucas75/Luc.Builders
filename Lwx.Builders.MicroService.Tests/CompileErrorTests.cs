using System;
using System.Collections.Generic;
using System.Linq;
using Lwx.Builders.MicroService.Tests;
using Microsoft.CodeAnalysis;
using Xunit;

public class CompileErrorTests
{
    [Fact]
    public void Combined_Naming_Path_Annotation_Diagnostics()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            // endpoint with bad name
            ["Endpoints/BadName.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp.Endpoints;

                [LwxEndpoint(Uri = "GET /hello", Summary = "hello", Publish = LwxStage.All)]
                public static partial class BadName
                {
                    public static Task<string> Execute() => Task.FromResult("hello");
                }
                """,

            // endpoint in wrong namespace (no '.Endpoints')
            ["WrongNamespace.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp.NotEndpoints;

                [LwxEndpoint(Uri = "GET /wrong", Summary = "wrong", Publish = LwxStage.All)]
                public static partial class EndpointWrong
                {
                    public static Task<string> Execute() => Task.FromResult("wrong");
                }
                """,

            // file path mismatch for endpoint
            ["WrongPath.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp.Deep.Nested.Endpoints;

                [LwxEndpoint(Uri = "GET /deep", Summary = "deep", Publish = LwxStage.All)]
                public static partial class EndpointDeep
                {
                    public static Task<string> Execute() => Task.FromResult("deep");
                }
                """,

            // endpoint in .Endpoints but missing annotation
            ["Endpoints/Unannotated.cs"] = $$"""
                namespace TestApp.Endpoints;

                public class UnannotatedEndpoint { }
                """,

            // worker with missing attribute
            ["Workers/Unannotated.cs"] = $$"""
                using Microsoft.Extensions.Hosting;
                namespace TestApp.Workers;

                public class UnannotatedWorker : BackgroundService
                {
                    protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken) => System.Threading.Tasks.Task.CompletedTask;
                }
                """,

            // worker file path mismatch
            ["WorkerWrongPath.cs"] = $$"""
                using Microsoft.Extensions.Hosting;
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp.BadPath.Workers;

                [LwxWorker(Name = "WrongPathWorker")]
                public partial class WorkerWrongPath : BackgroundService
                {
                    protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken) => System.Threading.Tasks.Task.CompletedTask;
                }
                """,
        };

        var result = GeneratorTestHarness.RunGenerator(sources);
        var diagIds = result.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Verify expected diagnostics
        Assert.Contains("LWX001", diagIds); // invalid endpoint name
        Assert.Contains("LWX002", diagIds); // endpoint in wrong namespace
        Assert.Contains("LWX007", diagIds); // file path mismatch
        Assert.Contains("LWX018", diagIds); // unannotated endpoint
        Assert.Contains("LWX019", diagIds); // unannotated worker
    }

    [Fact]
    public void Combined_ServiceLevel_Diagnostics()
    {
        // Scenario A: single Service with invalid signature + unexpected public method + PublishSwagger (causes LWX014, LWX015, LWX003)
        var sourcesA = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Microsoft.AspNetCore.Builder;
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp;

                [LwxService(Title = "Test", PublishSwagger = LwxStage.All)]
                public static partial class Service
                {
                    public static int Configure(WebApplicationBuilder builder) => 0;
                    public static void UnexpectedPublicMethod() { }
                }
                """
        };

        var resultA = GeneratorTestHarness.RunGenerator(sourcesA);
        var diagIdsA = resultA.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("LWX014", diagIdsA);
        Assert.Contains("LWX015", diagIdsA);
        Assert.Contains("LWX003", diagIdsA);

        // Scenario B: duplicate Service declarations to trigger LWX017
        var sourcesB = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp;

                [LwxService(Title = "Test1")]
                public static partial class Service { }
                """,

            ["ServiceBad.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributes;
                namespace TestApp.Other;

                [LwxService(Title = "Test2")]
                public static partial class ServiceBad { }
                """
        };

        var resultB = GeneratorTestHarness.RunGenerator(sourcesB);
        var diagIdsB = resultB.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("LWX017", diagIdsB);
    }

    [Fact]
    public void Combined_MissingService_Diagnostic()
    {
        var sources = new Dictionary<string, string>
        {
            ["NoService.cs"] = $$"""
                namespace TestApp;
                public class SomeClass { }
                """
        };

        var result = GeneratorTestHarness.RunGenerator(sources);
        Assert.True(GeneratorTestHarness.HasDiagnostic(result, "LWX011"), $"Expected LWX011 diagnostic. Got: {GeneratorTestHarness.FormatDiagnostics(result)}");
    }
}

