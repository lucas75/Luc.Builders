using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Lwx.Builders.MicroService.Tests.MockServices;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Lwx.Builders.MicroService.Tests;

[DisplayName("Compile Error Tests")]
public class CompileErrorTests
{
    /// <summary>
    /// Validates diagnostics for endpoints and workers with naming/path/annotation problems.
    /// Expects errors for invalid names, namespace placement, path mismatches, and unannotated members.
    /// </summary>
    [Fact(DisplayName = "Endpoints/Workers diagnostics: naming, path and annotation errors detected")]
    public void Combined_Naming_Path_Annotation_Diagnostics()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            // endpoint with bad name
            ["Endpoints/BadName.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public static partial class BadName
                {
                    [LwxEndpoint(Uri = "GET /hello", Summary = "hello", Publish = LwxStage.All)]
                    public static Task<string> Execute() => Task.FromResult("hello");
                }
                """,

            // endpoint in wrong namespace (no '.Endpoints')
            ["WrongNamespace.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.NotEndpoints;

                public static partial class EndpointWrong
                {
                    [LwxEndpoint(Uri = "GET /wrong", Summary = "wrong", Publish = LwxStage.All)]
                    public static Task<string> Execute() => Task.FromResult("wrong");
                }
                """,

            // file path mismatch for endpoint
            ["WrongPath.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Deep.Nested.Endpoints;

                public static partial class EndpointDeep
                {
                    [LwxEndpoint(Uri = "GET /deep", Summary = "deep", Publish = LwxStage.All)]
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
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.BadPath.Workers;

                [LwxWorker(Name = "WrongPathWorker")]
                public partial class WorkerWrongPath : BackgroundService
                {
                    protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken) => System.Threading.Tasks.Task.CompletedTask;
                }
                """,
        };

        var result = MockCompiler.RunGenerator(sources);
        var diagIds = result.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Verify expected diagnostics
        Assert.Contains("LWX001", diagIds); // invalid endpoint name
        Assert.Contains("LWX002", diagIds); // endpoint in wrong namespace
        Assert.Contains("LWX007", diagIds); // file path mismatch
        Assert.Contains("LWX018", diagIds); // unannotated endpoint
        Assert.Contains("LWX019", diagIds); // unannotated worker
    }

    /// <summary>
    /// Verifies service-level diagnostics like invalid Configure signatures, unexpected public methods,
    /// PublishSwagger usage, and duplicate service definitions in the same namespace.
    /// </summary>
    [Fact(DisplayName = "Service diagnostics: signature, swagger, unexpected methods, duplicate services")]
    public void Combined_ServiceLevel_Diagnostics()
    {
        // Scenario A: single Service with invalid signature + unexpected public method + PublishSwagger (causes LWX014, LWX015, LWX003)
        var sourcesA = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Microsoft.AspNetCore.Builder;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test", PublishSwagger = LwxStage.All)]
                public static partial class Service
                {
                    public static int Configure(WebApplicationBuilder builder) => 0;
                    public static void UnexpectedPublicMethod() { }
                }
                """
        };

        var resultA = MockCompiler.RunGenerator(sourcesA);
        var diagIdsA = resultA.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("LWX014", diagIdsA);
        Assert.Contains("LWX015", diagIdsA);
        Assert.Contains("LWX003", diagIdsA);

        // Scenario B: duplicate Service declarations in the SAME namespace to trigger LWX017
        // Note: Multiple services in DIFFERENT namespaces are now allowed (multi-service architecture)
        var sourcesB = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test1")]
                public static partial class Service { }
                """,

            ["ServiceDupe.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test2")]
                public static partial class ServiceDupe { }
                """
        };

        var resultB = MockCompiler.RunGenerator(sourcesB);
        var diagIdsB = resultB.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("LWX017", diagIdsB);
    }

    /// <summary>
    /// Ensures an error is reported when no Service declaration is present in a project with endpoints/workers.
    /// </summary>
    [Fact(DisplayName = "No Service: missing Service declaration reports diagnostic LWX011")]
    public void Combined_MissingService_Diagnostic()
    {
        var sources = new Dictionary<string, string>
        {
            ["NoService.cs"] = $$"""
                namespace TestApp;
                public class SomeClass { }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        Assert.True(MockCompiler.HasDiagnostic(result, "LWX011"), $"Expected LWX011 diagnostic. Got: {MockCompiler.FormatDiagnostics(result)}");
    }

    /// <summary>
    /// Confirms the generator produces Service helper methods and generated files for a valid Service.
    /// </summary>
    [Fact(DisplayName = "Generator: emits Service helper methods and files")]
    public void Generator_Generates_Service_Helpers()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Microsoft.AspNetCore.Builder;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test", PublishSwagger = LwxStage.None)]
                public static partial class Service { }
                """
        };

        var res = MockCompiler.RunGenerator(sources);
        // Expected generated filename is {safe(ns)}.Service.g.cs => TestApp.Service.g.cs
        Assert.Contains(res.GeneratedSources.Keys, k => k.EndsWith("TestApp.Service.g.cs", StringComparison.OrdinalIgnoreCase));
        var genContent = res.GeneratedSources.Values.FirstOrDefault(v => v.Contains("Configure(WebApplicationBuilder"));
        Assert.NotNull(genContent);
        Assert.Contains("Configure(WebApplicationBuilder", genContent);
        Assert.Contains("Configure(WebApplication", genContent);
    }
}

