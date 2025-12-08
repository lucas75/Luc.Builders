using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Lwx.Builders.MicroService.Tests.MockServices;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Lwx.Builders.MicroService.Tests;

[DisplayName("MessageEndpoint Tests")]
public class MessageEndpointTests
{
    /// <summary>
    /// Tests that a valid MessageEndpoint with all required properties generates code correctly.
    /// </summary>
    [Fact(DisplayName = "Valid MessageEndpoint generates hosted service and Configure methods")]
    public void Valid_MessageEndpoint_Generates_Code()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["Endpoints/EndpointMsgReceiveOrder.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class EndpointMsgReceiveOrder
                {
                    [LwxEndpoint(
                        "POST /receive-order",
                        Publish = LwxStage.DevelopmentOnly
                    )]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "OrderQueue",
                        QueueReaders = 4
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Filter out LWX007 (file path mismatch) since MockCompiler uses in-memory files
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "LWX007")
            .ToList();
        
        Assert.Empty(errors);

        // Should have generated the message endpoint file
        Assert.Contains(result.GeneratedSources.Keys, k => k.Contains("EndpointMsgReceiveOrder"));

        // Get the message endpoint generated file by key (filename)
        var endpointKey = result.GeneratedSources.Keys.FirstOrDefault(k => k.Contains("EndpointMsgReceiveOrder"));
        Assert.NotNull(endpointKey);
        
        var genContent = result.GeneratedSources[endpointKey!];
        Assert.NotNull(genContent);
        
        // Verify the generated code contains expected elements
        Assert.Contains("HostedService", genContent);
        Assert.Contains("Configure(WebApplicationBuilder builder)", genContent);
        Assert.Contains("Configure(WebApplication app)", genContent);
    }

    /// <summary>
    /// Tests that MessageEndpoint without QueueProvider emits LWX043 error.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint without QueueProvider emits LWX043")]
    public void MessageEndpoint_Without_QueueProvider_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/EndpointMsgTest.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class EndpointMsgTest
                {
                    [LwxEndpoint("POST /test", Publish = LwxStage.DevelopmentOnly)]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueConfigSection = "TestQueue"
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Should have LWX043 error
        var hasError = result.Diagnostics.Any(d => d.Id == "LWX043" && d.Severity == DiagnosticSeverity.Error);
        Assert.True(hasError, "Expected LWX043 error for missing QueueProvider");
    }

    /// <summary>
    /// Tests that MessageEndpoint with invalid HandlerErrorPolicy emits LWX047 error.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint with invalid HandlerErrorPolicy emits LWX047")]
    public void MessageEndpoint_InvalidHandlerErrorPolicy_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }

                public class NotAnErrorPolicy { }
                """,

            ["Endpoints/EndpointMsgTest.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class EndpointMsgTest
                {
                    [LwxEndpoint("POST /test", Publish = LwxStage.DevelopmentOnly)]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "TestQueue",
                        HandlerErrorPolicy = typeof(NotAnErrorPolicy)
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Should have LWX047 error
        var hasError = result.Diagnostics.Any(d => d.Id == "LWX047" && d.Severity == DiagnosticSeverity.Error);
        Assert.True(hasError, "Expected LWX047 error for invalid HandlerErrorPolicy");
    }

    /// <summary>
    /// Tests that MessageEndpoint with invalid ProviderErrorPolicy emits LWX049 error.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint with invalid ProviderErrorPolicy emits LWX049")]
    public void MessageEndpoint_InvalidProviderErrorPolicy_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }

                public class NotAProviderErrorPolicy { }
                """,

            ["Endpoints/EndpointMsgTest.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class EndpointMsgTest
                {
                    [LwxEndpoint("POST /test", Publish = LwxStage.DevelopmentOnly)]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "TestQueue",
                        ProviderErrorPolicy = typeof(NotAProviderErrorPolicy)
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Should have LWX049 error
        var hasError = result.Diagnostics.Any(d => d.Id == "LWX049" && d.Severity == DiagnosticSeverity.Error);
        Assert.True(hasError, "Expected LWX049 error for invalid ProviderErrorPolicy");
    }

    /// <summary>
    /// Tests that MessageEndpoint with invalid naming emits LWX040 error.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint with invalid naming emits LWX040")]
    public void MessageEndpoint_InvalidNaming_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["Endpoints/WronglyNamedEndpoint.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class WronglyNamedEndpoint
                {
                    [LwxEndpoint("POST /test", Publish = LwxStage.DevelopmentOnly)]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "TestQueue"
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Should have LWX040 error for invalid naming
        var hasError = result.Diagnostics.Any(d => d.Id == "LWX040" && d.Severity == DiagnosticSeverity.Error);
        Assert.True(hasError, "Expected LWX040 error for invalid naming");
    }

    /// <summary>
    /// Tests that MessageEndpoint outside Endpoints namespace emits LWX042 error.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint outside Endpoints namespace emits LWX042")]
    public void MessageEndpoint_WrongNamespace_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["WrongPlace/EndpointMsgTest.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                using TestApp.Endpoints;
                namespace TestApp.WrongPlace;

                public partial class EndpointMsgTest
                {
                    [LwxEndpoint("POST /test", Publish = LwxStage.DevelopmentOnly)]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "TestQueue"
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Should have LWX042 error for wrong namespace
        var hasError = result.Diagnostics.Any(d => d.Id == "LWX042" && d.Severity == DiagnosticSeverity.Error);
        Assert.True(hasError, "Expected LWX042 error for wrong namespace");
    }

    /// <summary>
    /// Tests that MessageEndpoint with DI parameters generates correct DI resolution.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint with DI parameters generates correct resolution code")]
    public void MessageEndpoint_WithDiParams_GeneratesResolution()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["Endpoints/EndpointMsgReceiveOrder.cs"] = """
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Microsoft.Extensions.Logging;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class EndpointMsgReceiveOrder
                {
                    [LwxEndpoint(
                        "POST /receive-order",
                        Publish = LwxStage.DevelopmentOnly
                    )]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "OrderQueue"
                    )]
                    public static Task Execute(ILwxQueueMessage msg, ILogger<EndpointMsgReceiveOrder> logger, IConfiguration config) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Filter out LWX007
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "LWX007")
            .ToList();
        Assert.Empty(errors);

        // Generated code should contain DI resolution
        var endpointKey = result.GeneratedSources.Keys.FirstOrDefault(k => k.Contains("EndpointMsgReceiveOrder"));
        Assert.NotNull(endpointKey);
        var genContent = result.GeneratedSources[endpointKey!];
        Assert.NotNull(genContent);
        Assert.Contains("GetRequiredService", genContent);
        Assert.Contains("ILogger", genContent);
        Assert.Contains("IConfiguration", genContent);
    }

    /// <summary>
    /// Tests that separate QueueStage and UriStage generate correct conditionals.
    /// </summary>
    [Fact(DisplayName = "MessageEndpoint with different QueueStage and UriStage generates correct conditionals")]
    public void MessageEndpoint_SeparateStages_GeneratesCorrectConditionals()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = """
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["Endpoints/ExampleQueueProvider.cs"] = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["Endpoints/EndpointMsgReceiveOrder.cs"] = """
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.Endpoints;

                public partial class EndpointMsgReceiveOrder
                {
                    [LwxEndpoint(
                        "POST /receive-order",
                        Publish = LwxStage.DevelopmentOnly
                    )]
                    [LwxMessageSource(
                        Stage = LwxStage.All,
                        QueueProvider = typeof(ExampleQueueProvider),
                        QueueConfigSection = "OrderQueue"
                    )]
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Filter out LWX007
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "LWX007")
            .ToList();
        Assert.Empty(errors);

        // Generated code should reference both stages
        var endpointKey = result.GeneratedSources.Keys.FirstOrDefault(k => k.Contains("EndpointMsgReceiveOrder"));
        Assert.NotNull(endpointKey);
        var genContent = result.GeneratedSources[endpointKey!];
        Assert.NotNull(genContent);
        Assert.Contains("QueueStage=", genContent);
        Assert.Contains("UriStage=", genContent);
    }
}
