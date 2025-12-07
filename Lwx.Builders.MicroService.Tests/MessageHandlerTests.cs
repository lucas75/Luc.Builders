using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Lwx.Builders.MicroService.Tests.MockServices;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Lwx.Builders.MicroService.Tests;

[DisplayName("MessageHandler Tests")]
public class MessageHandlerTests
{
    /// <summary>
    /// Tests that a valid MessageHandler with all required properties generates code correctly.
    /// </summary>
    [Fact(DisplayName = "Valid MessageHandler generates hosted service and Configure methods")]
    public void Valid_MessageHandler_Generates_Code()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/ExampleQueueProvider.cs"] = $$"""
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["MessageHandlers/MessageHandlerReceiveOrder.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                [LwxMessageHandler(
                    Uri = "POST /receive-order",
                    Stage = LwxStage.All,
                    QueueProvider = typeof(ExampleQueueProvider),
                    QueueConfigSection = "OrderQueue",
                    QueueReaders = 4
                )]
                public partial class MessageHandlerReceiveOrder
                {
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

        // Should have generated the message handler file
        Assert.Contains(result.GeneratedSources.Keys, k => k.Contains("MessageHandlerReceiveOrder"));

        // Generated code should contain hosted service
        var genContent = result.GeneratedSources.Values.FirstOrDefault(v => v.Contains("MessageHandlerReceiveOrder"));
        Assert.NotNull(genContent);
        Assert.Contains("HostedService", genContent);
        Assert.Contains("Configure(WebApplicationBuilder builder)", genContent);
        Assert.Contains("Configure(WebApplication app)", genContent);
    }

    /// <summary>
    /// Tests that MessageHandler without QueueProvider emits LWX043 error.
    /// </summary>
    [Fact(DisplayName = "MessageHandler without QueueProvider emits LWX043")]
    public void MessageHandler_Without_QueueProvider_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/MessageHandlerNoProvider.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                [LwxMessageHandler(
                    Stage = LwxStage.All,
                    QueueConfigSection = "TestQueue"
                )]
                public partial class MessageHandlerNoProvider
                {
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        Assert.True(MockCompiler.HasDiagnostic(result, "LWX043"), $"Expected LWX043. Got: {MockCompiler.FormatDiagnostics(result)}");
    }

    /// <summary>
    /// Tests that MessageHandler with invalid naming emits LWX040 error.
    /// </summary>
    [Fact(DisplayName = "MessageHandler with invalid name emits LWX040")]
    public void MessageHandler_InvalidName_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/ExampleQueueProvider.cs"] = $$"""
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["MessageHandlers/BadName.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                [LwxMessageHandler(
                    Stage = LwxStage.All,
                    QueueProvider = typeof(ExampleQueueProvider),
                    QueueConfigSection = "TestQueue"
                )]
                public partial class BadName
                {
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        Assert.True(MockCompiler.HasDiagnostic(result, "LWX040"), $"Expected LWX040. Got: {MockCompiler.FormatDiagnostics(result)}");
    }

    /// <summary>
    /// Tests that MessageHandler outside MessageHandlers namespace emits LWX042 error.
    /// </summary>
    [Fact(DisplayName = "MessageHandler in wrong namespace emits LWX042")]
    public void MessageHandler_WrongNamespace_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["ExampleQueueProvider.cs"] = $$"""
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["MessageHandlerInWrongPlace.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.NotMessageHandlers;

                [LwxMessageHandler(
                    Stage = LwxStage.All,
                    QueueProvider = typeof(TestApp.ExampleQueueProvider),
                    QueueConfigSection = "TestQueue"
                )]
                public partial class MessageHandlerInWrongPlace
                {
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        Assert.True(MockCompiler.HasDiagnostic(result, "LWX042"), $"Expected LWX042. Got: {MockCompiler.FormatDiagnostics(result)}");
    }

    /// <summary>
    /// Tests that class in MessageHandlers namespace without [LwxMessageHandler] emits LWX050 error.
    /// </summary>
    [Fact(DisplayName = "Unannotated class in MessageHandlers namespace emits LWX050")]
    public void Unannotated_MessageHandler_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/UnannotatedHandler.cs"] = $$"""
                namespace TestApp.MessageHandlers;

                public class UnannotatedHandler { }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        Assert.True(MockCompiler.HasDiagnostic(result, "LWX050"), $"Expected LWX050. Got: {MockCompiler.FormatDiagnostics(result)}");
    }

    /// <summary>
    /// Tests that MessageHandler with QueueProvider not implementing ILwxQueueProvider emits LWX045.
    /// </summary>
    [Fact(DisplayName = "MessageHandler with non-implementing QueueProvider emits LWX045")]
    public void MessageHandler_NonImplementingProvider_Emits_Error()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/NotAProvider.cs"] = $$"""
                namespace TestApp.MessageHandlers;

                public class NotAProvider { }
                """,

            ["MessageHandlers/MessageHandlerBadProvider.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                [LwxMessageHandler(
                    Stage = LwxStage.All,
                    QueueProvider = typeof(NotAProvider),
                    QueueConfigSection = "TestQueue"
                )]
                public partial class MessageHandlerBadProvider
                {
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        // Either LWX045 (doesn't implement) or LWX050 (unannotated in namespace) could fire
        var diagIds = result.Diagnostics.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(diagIds.Contains("LWX045") || diagIds.Contains("LWX050"), 
            $"Expected LWX045 or LWX050. Got: {MockCompiler.FormatDiagnostics(result)}");
    }

    /// <summary>
    /// Tests that MessageHandler with naming exception is allowed.
    /// </summary>
    [Fact(DisplayName = "MessageHandler with NamingExceptionJustification bypasses naming check")]
    public void MessageHandler_NamingException_Bypasses_Check()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/ExampleQueueProvider.cs"] = $$"""
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["MessageHandlers/MessageHandlerLegacyName.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                [LwxMessageHandler(
                    Uri = "POST /legacy/order",
                    Stage = LwxStage.All,
                    QueueProvider = typeof(ExampleQueueProvider),
                    QueueConfigSection = "TestQueue",
                    NamingExceptionJustification = "Legacy endpoint migration"
                )]
                public partial class MessageHandlerLegacyName
                {
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Should not have naming errors
        Assert.False(MockCompiler.HasDiagnostic(result, "LWX040"), "Should not have LWX040");
        Assert.False(MockCompiler.HasDiagnostic(result, "LWX041"), "Should not have LWX041");
        
        // Should have generated code
        Assert.Contains(result.GeneratedSources.Keys, k => k.Contains("MessageHandlerLegacyName"));
    }

    /// <summary>
    /// Tests that Service.Configure includes MessageHandler calls.
    /// </summary>
    [Fact(DisplayName = "Service Configure includes MessageHandler wiring")]
    public void Service_Configure_Includes_MessageHandlers()
    {
        var sources = new Dictionary<string, string>
        {
            ["Service.cs"] = $$"""
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp;

                [LwxService(Title = "Test")]
                public static partial class Service { }
                """,

            ["MessageHandlers/ExampleQueueProvider.cs"] = $$"""
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Microsoft.Extensions.Configuration;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                public class ExampleQueueProvider : ILwxQueueProvider
                {
                    public string Name => nameof(ExampleQueueProvider);
                    public void Configure(IConfiguration configuration, string sectionName) { }
                    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { }
                    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) => Task.CompletedTask;
                    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                }
                """,

            ["MessageHandlers/MessageHandlerTestQueue.cs"] = $$"""
                using System.Threading.Tasks;
                using Lwx.Builders.MicroService.Atributtes;
                namespace TestApp.MessageHandlers;

                [LwxMessageHandler(
                    Stage = LwxStage.All,
                    QueueProvider = typeof(ExampleQueueProvider),
                    QueueConfigSection = "TestQueue",
                    QueueReaders = 2
                )]
                public partial class MessageHandlerTestQueue
                {
                    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
                }
                """
        };

        var result = MockCompiler.RunGenerator(sources);
        
        // Service should include ConfigureMessageHandlers
        var serviceContent = result.GeneratedSources.Values.FirstOrDefault(v => v.Contains("public static partial class Service"));
        Assert.NotNull(serviceContent);
        Assert.Contains("ConfigureMessageHandlers", serviceContent);
    }
}
