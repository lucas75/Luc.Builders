using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxMessageHandlerTypeProcessor(
    Generator parent,
    Compilation compilation,
    SourceProductionContext ctx,
    AttributeInstance attr
)
{
    public void Execute()
    {
        // Enforce file path and namespace matching
        ProcessorUtils.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);

        var name = ProcessorUtils.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        // Extract attribute properties
        var (uriArg, stageLiteral, queueProviderTypeName, queueConfigSection, queueReaders, 
             handlerErrorPolicyTypeName, providerErrorPolicyTypeName, summary, description, namingException) 
            = ExtractAttributeMetadata();

        // Validate naming convention
        if (!ValidateHandlerNaming(uriArg, name, ns, namingException))
        {
            return;
        }

        // Validate QueueProvider implements ILwxQueueProvider
        if (!ValidateQueueProvider(queueProviderTypeName))
        {
            return;
        }

        // Validate error policies
        if (!ValidateErrorPolicies(handlerErrorPolicyTypeName, providerErrorPolicyTypeName))
        {
            return;
        }

        // Validate namespace is under MessageHandlers
        if (!ValidateNamespace(ns))
        {
            return;
        }

        // Generate the hosted service and Configure method
        GenerateSourceFiles(name, ns, uriArg, stageLiteral, queueProviderTypeName, queueConfigSection, 
                           queueReaders, handlerErrorPolicyTypeName, providerErrorPolicyTypeName, 
                           summary, description, namingException);

        // Register with service
        var servicePrefix = Generator.ComputeServicePrefix(ns);
        var reg = parent.GetOrCreateRegistration(servicePrefix);
        reg.MessageHandlerNames.Add(ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation));
        reg.MessageHandlerInfos.Add((
            ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation),
            queueReaders,
            queueConfigSection ?? string.Empty,
            uriArg ?? string.Empty
        ));
    }

    private (string? uri, string stageLiteral, string? queueProviderTypeName, string? queueConfigSection, 
             int queueReaders, string? handlerErrorPolicyTypeName, string? providerErrorPolicyTypeName,
             string? summary, string? description, string? namingException) ExtractAttributeMetadata()
    {
        string? uri = null;
        string stageLiteral = "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
        string? queueProviderTypeName = null;
        string? queueConfigSection = null;
        int queueReaders = 2;
        string? handlerErrorPolicyTypeName = null;
        string? providerErrorPolicyTypeName = null;
        string? summary = null;
        string? description = null;
        string? namingException = null;

        if (attr.AttributeData == null) 
            return (uri, stageLiteral, queueProviderTypeName, queueConfigSection, queueReaders,
                    handlerErrorPolicyTypeName, providerErrorPolicyTypeName, summary, description, namingException);

        var named = attr.AttributeData.ToNamedArgumentMap();

        // Uri (also check constructor arg)
        if (named.TryGetValue("Uri", out var uriTc) && uriTc.Value is string s)
        {
            uri = s;
        }
        else if (attr.AttributeData.ConstructorArguments.Length > 0 && attr.AttributeData.ConstructorArguments[0].Value is string cs)
        {
            uri = cs;
        }

        // Stage
        if (named.TryGetValue("Stage", out var stageTc) && stageTc.Value != null)
        {
            var raw = stageTc.Value;
            if (raw is int iv)
            {
                stageLiteral = iv switch
                {
                    1 => "Lwx.Builders.MicroService.Atributtes.LwxStage.DevelopmentOnly",
                    2 => "Lwx.Builders.MicroService.Atributtes.LwxStage.All",
                    _ => "Lwx.Builders.MicroService.Atributtes.LwxStage.None"
                };
            }
            else
            {
                var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
                stageLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributtes.LwxStage." + tmp);
            }
        }

        // QueueProvider
        if (named.TryGetValue("QueueProvider", out var qpTc) && qpTc.Value is INamedTypeSymbol qpType)
        {
            queueProviderTypeName = qpType.ToDisplayString();
        }

        // QueueConfigSection
        if (named.TryGetValue("QueueConfigSection", out var qcsTc) && qcsTc.Value is string qcs)
        {
            queueConfigSection = qcs;
        }

        // QueueReaders
        if (named.TryGetValue("QueueReaders", out var qrTc) && qrTc.Value is int qr)
        {
            queueReaders = qr;
        }

        // HandlerErrorPolicy
        if (named.TryGetValue("HandlerErrorPolicy", out var hepTc) && hepTc.Value is INamedTypeSymbol hepType)
        {
            handlerErrorPolicyTypeName = hepType.ToDisplayString();
        }

        // ProviderErrorPolicy
        if (named.TryGetValue("ProviderErrorPolicy", out var pepTc) && pepTc.Value is INamedTypeSymbol pepType)
        {
            providerErrorPolicyTypeName = pepType.ToDisplayString();
        }

        // Summary
        if (named.TryGetValue("Summary", out var sumTc) && sumTc.Value is string sumVal)
        {
            summary = sumVal;
        }

        // Description
        if (named.TryGetValue("Description", out var descTc) && descTc.Value is string descVal)
        {
            description = descVal;
        }

        // NamingExceptionJustification
        if (named.TryGetValue("NamingExceptionJustification", out var neTc) && neTc.Value is string neVal)
        {
            namingException = neVal?.Trim();
        }

        return (uri, stageLiteral, queueProviderTypeName, queueConfigSection, queueReaders,
                handlerErrorPolicyTypeName, providerErrorPolicyTypeName, summary, description, namingException);
    }

    private bool ValidateHandlerNaming(string? uriArg, string name, string ns, string? namingException)
    {
        // Naming convention: MessageHandler{PathSegments} for URI /path/segments
        // If no URI, just require name starts with "MessageHandler"
        if (!name.StartsWith("MessageHandler", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(namingException))
            {
                return true; // Exception provided
            }

            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX040",
                    "Invalid message handler class name",
                    "Message handler class '{0}' must start with 'MessageHandler'. Example: MessageHandlerReceiveOrder",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, attr.TargetSymbol.Name));
            return false;
        }

        if (!string.IsNullOrEmpty(uriArg))
        {
            // Validate name matches URI pattern
            var verbParts = uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var path = verbParts.Length > 1 ? verbParts[1] : verbParts[0];
            var segs = path.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var nameParts = new List<string>();
            foreach (var seg in segs)
            {
                if (seg.StartsWith("{") && seg.EndsWith("}"))
                {
                    var pname = seg.Substring(1, seg.Length - 2);
                    nameParts.Add("Param" + ProcessorUtils.PascalSafe(pname));
                }
                else
                {
                    nameParts.Add(ProcessorUtils.PascalSafe(seg));
                }
            }

            var expectedName = "MessageHandler" + string.Join(string.Empty, nameParts);

            if (!string.Equals(name, expectedName, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(namingException))
                {
                    return true; // Exception provided
                }

                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX041",
                        "Message handler name does not match URI",
                        "Message handler class '{0}' does not match expected name '{1}' for URI '{2}'",
                        "Naming",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location, attr.TargetSymbol.Name, expectedName, uriArg));
                return false;
            }
        }

        return true;
    }

    private bool ValidateNamespace(string ns)
    {
        if (!ns.Contains(".MessageHandlers", StringComparison.Ordinal))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX042",
                    "Invalid message handler namespace",
                    "Message handler class must be in a namespace containing '.MessageHandlers'. Found: '{0}'",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, ns));
            return false;
        }

        return true;
    }

    private bool ValidateQueueProvider(string? queueProviderTypeName)
    {
        if (string.IsNullOrEmpty(queueProviderTypeName))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX043",
                    "Missing QueueProvider",
                    "Message handler '{0}' must specify a QueueProvider type that implements ILwxQueueProvider",
                    "Configuration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, attr.TargetSymbol.Name));
            return false;
        }

        // Verify the type implements ILwxQueueProvider
        var providerType = compilation.GetTypeByMetadataName(queueProviderTypeName);
        if (providerType == null)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX044",
                    "QueueProvider type not found",
                    "QueueProvider type '{0}' could not be found in the compilation",
                    "Configuration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, queueProviderTypeName));
            return false;
        }

        // Check if it implements ILwxQueueProvider
        var queueProviderInterface = compilation.GetTypeByMetadataName("Lwx.Builders.MicroService.Atributtes.ILwxQueueProvider");
        if (queueProviderInterface != null)
        {
            var implements = providerType.AllInterfaces.Any(i => 
                SymbolEqualityComparer.Default.Equals(i, queueProviderInterface) ||
                i.ToDisplayString() == "Lwx.Builders.MicroService.Atributtes.ILwxQueueProvider");

            if (!implements)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX045",
                        "QueueProvider must implement ILwxQueueProvider",
                        "QueueProvider type '{0}' does not implement ILwxQueueProvider",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location, queueProviderTypeName));
                return false;
            }
        }

        return true;
    }

    private bool ValidateErrorPolicies(string? handlerErrorPolicyTypeName, string? providerErrorPolicyTypeName)
    {
        // Validate handler error policy if specified
        if (!string.IsNullOrEmpty(handlerErrorPolicyTypeName))
        {
            var policyType = compilation.GetTypeByMetadataName(handlerErrorPolicyTypeName);
            if (policyType == null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX046",
                        "HandlerErrorPolicy type not found",
                        "HandlerErrorPolicy type '{0}' could not be found in the compilation",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location, handlerErrorPolicyTypeName));
                return false;
            }

            var errorPolicyInterface = compilation.GetTypeByMetadataName("Lwx.Builders.MicroService.Atributtes.ILwxErrorPolicy");
            if (errorPolicyInterface != null)
            {
                var implements = policyType.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, errorPolicyInterface) ||
                    i.ToDisplayString() == "Lwx.Builders.MicroService.Atributtes.ILwxErrorPolicy");

                if (!implements)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "LWX047",
                            "HandlerErrorPolicy must implement ILwxErrorPolicy",
                            "HandlerErrorPolicy type '{0}' does not implement ILwxErrorPolicy",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        attr.Location, handlerErrorPolicyTypeName));
                    return false;
                }
            }
        }

        // Validate provider error policy if specified
        if (!string.IsNullOrEmpty(providerErrorPolicyTypeName))
        {
            var policyType = compilation.GetTypeByMetadataName(providerErrorPolicyTypeName);
            if (policyType == null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX048",
                        "ProviderErrorPolicy type not found",
                        "ProviderErrorPolicy type '{0}' could not be found in the compilation",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location, providerErrorPolicyTypeName));
                return false;
            }

            var providerErrorPolicyInterface = compilation.GetTypeByMetadataName("Lwx.Builders.MicroService.Atributtes.ILwxProviderErrorPolicy");
            if (providerErrorPolicyInterface != null)
            {
                var implements = policyType.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, providerErrorPolicyInterface) ||
                    i.ToDisplayString() == "Lwx.Builders.MicroService.Atributtes.ILwxProviderErrorPolicy");

                if (!implements)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "LWX049",
                            "ProviderErrorPolicy must implement ILwxProviderErrorPolicy",
                            "ProviderErrorPolicy type '{0}' does not implement ILwxProviderErrorPolicy",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        attr.Location, providerErrorPolicyTypeName));
                    return false;
                }
            }
        }

        return true;
    }

    private void GenerateSourceFiles(
        string name, string ns, string? uriArg, string stageLiteral,
        string? queueProviderTypeName, string? queueConfigSection, int queueReaders,
        string? handlerErrorPolicyTypeName, string? providerErrorPolicyTypeName,
        string? summary, string? description, string? namingException)
    {
        var shortStage = stageLiteral.Contains('.')
            ? string.Join('.', stageLiteral.Split('.').Skip(Math.Max(0, stageLiteral.Split('.').Length - 2)))
            : stageLiteral;

        // Default policies
        var effectiveHandlerErrorPolicy = handlerErrorPolicyTypeName ?? "LwxDefaultErrorPolicy";
        var effectiveProviderErrorPolicy = providerErrorPolicyTypeName ?? "LwxDefaultProviderErrorPolicy";

        // Extract HTTP details if URI is provided
        string? httpVerb = null;
        string? pathPart = null;
        if (!string.IsNullOrEmpty(uriArg))
        {
            var parts = uriArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            httpVerb = parts.Length > 0 ? parts[0].ToUpperInvariant() : "POST";
            pathPart = parts.Length > 1 ? parts[1] : (parts.Length > 0 ? parts[0] : string.Empty);
        }

        string configureMethod;
        if (stageLiteral.EndsWith(".None", StringComparison.Ordinal))
        {
            configureMethod = $$"""
public static void Configure(WebApplicationBuilder builder)
{
    // Stage={{shortStage}} - Handler is disabled
}

public static void Configure(WebApplication app)
{
    // Stage={{shortStage}} - Handler is disabled
}
""";
        }
        else
        {
            var condExpr = stageLiteral.EndsWith(".DevelopmentOnly", StringComparison.Ordinal)
                ? "builder.Environment.IsDevelopment()"
                : "builder.Environment.IsDevelopment() || builder.Environment.IsProduction()";

            var appCondExpr = stageLiteral.EndsWith(".DevelopmentOnly", StringComparison.Ordinal)
                ? "app.Environment.IsDevelopment()"
                : "app.Environment.IsDevelopment() || app.Environment.IsProduction()";

            var httpEndpointCode = string.Empty;
            if (!string.IsNullOrEmpty(pathPart))
            {
                var mapMethod = (httpVerb ?? "POST") switch
                {
                    "GET" => "MapGet",
                    "POST" => "MapPost",
                    "PUT" => "MapPut",
                    "DELETE" => "MapDelete",
                    _ => "MapPost"
                };

                httpEndpointCode = $$"""

        // Also expose HTTP endpoint for direct message injection
        var endpoint = app.{{mapMethod}}("{{pathPart}}", async (HttpContext ctx) =>
        {
            using var reader = new System.IO.StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            var msg = new LwxHttpQueueMessage(body, ctx.Request.Headers);
            await Execute(msg);
            return Results.Ok();
        });
        endpoint.WithName("{{name}}");
        {{(!string.IsNullOrEmpty(summary) ? $"endpoint.WithDisplayName(\"{GeneratorUtils.EscapeForCSharp(summary)}\");" : string.Empty)}}
""";
            }

            configureMethod = $$"""
public static void Configure(WebApplicationBuilder builder)
{
    // Stage={{shortStage}}
    if ({{condExpr}})
    {
        // Register descriptor for health/monitoring
        builder.Services.AddSingleton(new LwxMessageHandlerDescriptor
        {
            Name = "{{GeneratorUtils.EscapeForCSharp(name)}}",
            Description = "{{GeneratorUtils.EscapeForCSharp(description ?? string.Empty)}}",
            QueueReaders = {{queueReaders}},
            QueueConfigSection = "{{GeneratorUtils.EscapeForCSharp(queueConfigSection ?? string.Empty)}}",
            QueueProviderType = typeof({{queueProviderTypeName}}),
            HttpUri = {{(string.IsNullOrEmpty(pathPart) ? "null" : $"\"{GeneratorUtils.EscapeForCSharp(pathPart)}\"")}}
        });

        // Register the hosted service that manages the message handler
        builder.Services.AddHostedService<{{name}}HostedService>();
    }
}

public static void Configure(WebApplication app)
{
    // Stage={{shortStage}}
    if ({{appCondExpr}})
    {{{httpEndpointCode}}
    }
}
""";
        }

        // Generate the hosted service class
        var hostedServiceClass = string.Empty;
        if (!stageLiteral.EndsWith(".None", StringComparison.Ordinal))
        {
            hostedServiceClass = $$"""

/// <summary>
/// Hosted service that manages the message handler lifecycle.
/// </summary>
public sealed class {{name}}HostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<{{name}}HostedService> _logger;
    private ILwxQueueProvider? _provider;

    public {{name}}HostedService(IConfiguration configuration, ILogger<{{name}}HostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _provider = new {{queueProviderTypeName}}();
        var errorPolicy = new {{effectiveHandlerErrorPolicy}}();
        var providerErrorPolicy = new {{effectiveProviderErrorPolicy}}();

        _provider.Configure(_configuration, "{{GeneratorUtils.EscapeForCSharp(queueConfigSection ?? string.Empty)}}");
        _provider.SetProviderErrorPolicy(providerErrorPolicy);

        _logger.LogInformation(
            "Starting message handler {Name} with section {Section} and concurrency {Concurrency}",
            _provider.Name,
            "{{GeneratorUtils.EscapeForCSharp(queueConfigSection ?? string.Empty)}}",
            {{queueReaders}});

        async Task HandlerWrapper(ILwxQueueMessage msg, CancellationToken ct)
        {
            try
            {
                await {{name}}.Execute(msg);
                await msg.CompleteAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", msg.MessageId);
                await errorPolicy.HandleErrorAsync(msg, ex, ct);
            }
        }

        await _provider.StartAsync(HandlerWrapper, {{queueReaders}}, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_provider != null)
        {
            _logger.LogInformation("Stopping message handler {Name}", _provider.Name);
            await _provider.StopAsync(cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }
}
""";
        }

        // Generate helper class for HTTP message wrapping
        var httpMessageClass = string.Empty;
        if (!string.IsNullOrEmpty(pathPart) && !stageLiteral.EndsWith(".None", StringComparison.Ordinal))
        {
            httpMessageClass = $$"""

/// <summary>
/// Wraps an HTTP request body as an ILwxQueueMessage for unified processing.
/// </summary>
internal sealed class LwxHttpQueueMessage : ILwxQueueMessage
{
    private readonly string _body;
    private readonly IReadOnlyDictionary<string, string> _headers;

    public LwxHttpQueueMessage(string body, IHeaderDictionary requestHeaders)
    {
        _body = body;
        MessageId = Guid.NewGuid().ToString("N");
        EnqueuedAt = DateTimeOffset.UtcNow;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in requestHeaders)
        {
            dict[h.Key] = h.Value.ToString();
        }
        _headers = dict;
    }

    public string MessageId { get; }
    public ReadOnlyMemory<byte> Payload => System.Text.Encoding.UTF8.GetBytes(_body);
    public IReadOnlyDictionary<string, string> Headers => _headers;
    public DateTimeOffset EnqueuedAt { get; }

    public ValueTask CompleteAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask AbandonAsync(string? reason = null, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask DeadLetterAsync(string? reason = null, CancellationToken ct = default) => ValueTask.CompletedTask;
}
""";
        }

        var source = $$"""
// <auto-generated/>
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;

namespace {{ns}};

public partial class {{name}}
{
    {{configureMethod.FixIndent(1, indentFirstLine: false)}}
}
{{hostedServiceClass}}
{{httpMessageClass}}
""";

        var generatedFileName = !string.IsNullOrEmpty(namingException)
            ? $"{ns}.{name}.g.cs"
            : $"LwxMessageHandler_{name}.g.cs";

        ProcessorUtils.AddGeneratedFile(ctx, generatedFileName, source);
    }
}
