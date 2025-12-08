using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxMessageSourceTypeProcessor(
    Generator parent,
    Compilation compilation,
    SourceProductionContext ctx,
    AttributeInstance attr
)
{
    private INamedTypeSymbol? _containingType;
    private IMethodSymbol? _methodSymbol;

    public void Execute()
    {
        // The attribute is now on the Execute method, get the containing class
        if (attr.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX072",
                    "LwxMessageSource must be on method",
                    "[LwxMessageSource] attribute must be placed on the Execute method, not on a class.",
                    "Usage",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location));
            return;
        }

        // Validate method is named Execute
        if (methodSymbol.Name != "Execute")
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX073",
                    "LwxMessageSource must be on Execute method",
                    "[LwxMessageSource] attribute must be placed on a method named 'Execute'. Found: '{0}'",
                    "Usage",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, methodSymbol.Name));
            return;
        }

        _methodSymbol = methodSymbol;
        _containingType = methodSymbol.ContainingType;
        if (_containingType == null)
        {
            return;
        }

        // Enforce file path and namespace matching
        ProcessorUtils.ValidateFilePathMatchesNamespace(_containingType, ctx);

        var name = ProcessorUtils.SafeIdentifier(_containingType.Name);
        var ns = _containingType.ContainingNamespace?.ToDisplayString() ?? "Generated";

        // Extract attribute properties from LwxMessageSource
        var (queueStageLiteral, queueProviderTypeName, queueConfigSection, queueReaders, 
             handlerErrorPolicyTypeName, providerErrorPolicyTypeName) 
            = ExtractMessageSourceMetadata();

        // Extract LwxEndpoint attribute properties from the same method
        var (uriArg, uriStageLiteral, summary, description, namingException) = ExtractEndpointMetadata();

        // Validate naming convention - must start with EndpointMsg
        if (!ValidateEndpointNaming(uriArg, name, ns, namingException))
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

        // Validate namespace is under Endpoints
        if (!ValidateNamespace(ns))
        {
            return;
        }

        // Analyze the Execute method parameters for DI
        var executeParams = AnalyzeExecuteMethod();

        // Generate the hosted service and Configure method
        GenerateSourceFiles(name, ns, uriArg, queueStageLiteral, uriStageLiteral, queueProviderTypeName, queueConfigSection, 
                           queueReaders, handlerErrorPolicyTypeName, providerErrorPolicyTypeName, 
                           summary, description, namingException, executeParams);

        // Register with service
        var servicePrefix = Generator.ComputeServicePrefix(ns);
        var reg = parent.GetOrCreateRegistration(servicePrefix);
        reg.MessageEndpointNames.Add(ProcessorUtils.ExtractRelativeTypeName(_containingType!, compilation));
        reg.MessageEndpointInfos.Add((
            ProcessorUtils.ExtractRelativeTypeName(_containingType!, compilation),
            queueReaders,
            queueConfigSection ?? string.Empty,
            uriArg ?? string.Empty
        ));
    }

    private (string queueStageLiteral, string? queueProviderTypeName, string? queueConfigSection, 
             int queueReaders, string? handlerErrorPolicyTypeName, string? providerErrorPolicyTypeName) 
        ExtractMessageSourceMetadata()
    {
        string queueStageLiteral = "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
        string? queueProviderTypeName = null;
        string? queueConfigSection = null;
        int queueReaders = 2;
        string? handlerErrorPolicyTypeName = null;
        string? providerErrorPolicyTypeName = null;

        if (attr.AttributeData == null) 
            return (queueStageLiteral, queueProviderTypeName, queueConfigSection, queueReaders,
                    handlerErrorPolicyTypeName, providerErrorPolicyTypeName);

        var named = attr.AttributeData.ToNamedArgumentMap();

        // Stage (queue stage)
        if (named.TryGetValue("Stage", out var stageTc) && stageTc.Value != null)
        {
            queueStageLiteral = ParseStageLiteral(stageTc.Value);
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

        return (queueStageLiteral, queueProviderTypeName, queueConfigSection, queueReaders,
                handlerErrorPolicyTypeName, providerErrorPolicyTypeName);
    }

    private (string? uri, string uriStageLiteral, string? summary, string? description, string? namingException) 
        ExtractEndpointMetadata()
    {
        string? uri = null;
        string uriStageLiteral = "Lwx.Builders.MicroService.Atributtes.LwxStage.DevelopmentOnly";
        string? summary = null;
        string? description = null;
        string? namingException = null;

        if (_methodSymbol == null)
            return (uri, uriStageLiteral, summary, description, namingException);

        // Find LwxEndpoint attribute on the same method
        var endpointAttr = _methodSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "LwxEndpointAttribute" or "LwxEndpoint");

        if (endpointAttr == null)
            return (uri, uriStageLiteral, summary, description, namingException);

        var named = endpointAttr.ToNamedArgumentMap();

        // Uri from constructor arg or named property
        if (named.TryGetValue("Uri", out var uriTc) && uriTc.Value is string s)
        {
            uri = s;
        }
        else if (endpointAttr.ConstructorArguments.Length > 0 && endpointAttr.ConstructorArguments[0].Value is string cs)
        {
            uri = cs;
        }

        // Publish (uri stage)
        if (named.TryGetValue("Publish", out var publishTc) && publishTc.Value != null)
        {
            uriStageLiteral = ParseStageLiteral(publishTc.Value);
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

        return (uri, uriStageLiteral, summary, description, namingException);
    }

    private static string ParseStageLiteral(object raw)
    {
        if (raw is int iv)
        {
            return iv switch
            {
                1 => "Lwx.Builders.MicroService.Atributtes.LwxStage.DevelopmentOnly",
                2 => "Lwx.Builders.MicroService.Atributtes.LwxStage.All",
                _ => "Lwx.Builders.MicroService.Atributtes.LwxStage.None"
            };
        }
        else
        {
            var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
            return tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributtes.LwxStage." + tmp);
        }
    }

    private bool ValidateEndpointNaming(string? uriArg, string name, string ns, string? namingException)
    {
        // Naming convention: EndpointMsg{PathSegments} for URI /path/segments
        if (!name.StartsWith("EndpointMsg", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(namingException))
            {
                return true; // Exception provided
            }

            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX040",
                    "Invalid message endpoint class name",
                    "Message endpoint class '{0}' must start with 'EndpointMsg'. Example: EndpointMsgReceiveOrder",
                    "Naming",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, name));
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

            var expectedName = "EndpointMsg" + string.Join(string.Empty, nameParts);

            if (!string.Equals(name, expectedName, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(namingException))
                {
                    return true; // Exception provided
                }

                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX041",
                        "Message endpoint name does not match URI",
                        "Message endpoint class '{0}' does not match expected name '{1}' for URI '{2}'",
                        "Naming",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location, name, expectedName, uriArg));
                return false;
            }
        }

        return true;
    }

    private bool ValidateNamespace(string ns)
    {
        // MessageEndpoints should be in .Endpoints namespace (not .MessageHandlers)
        if (!ns.Contains(".Endpoints", StringComparison.Ordinal))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX042",
                    "Invalid message endpoint namespace",
                    "Message endpoint class must be in a namespace containing '.Endpoints'. Found: '{0}'",
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
                    "Message endpoint '{0}' must specify a QueueProvider type that implements ILwxQueueProvider",
                    "Configuration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location, _containingType?.Name ?? "unknown"));
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

    private List<(string ParamName, string ParamType, bool IsQueueMessage)> AnalyzeExecuteMethod()
    {
        var result = new List<(string ParamName, string ParamType, bool IsQueueMessage)>();
        
        if (_containingType == null)
            return result;

        var executeMethods = _containingType.GetMembers("Execute")
            .OfType<IMethodSymbol>()
            .Where(m => m.IsStatic)
            .ToList();

        if (executeMethods.Count == 0)
            return result;

        var executeMethod = executeMethods[0];
        foreach (var param in executeMethod.Parameters)
        {
            var paramType = param.Type.ToDisplayString();
            var isQueueMessage = paramType == "Lwx.Builders.MicroService.Atributtes.ILwxQueueMessage" ||
                                 param.Type.AllInterfaces.Any(i => i.ToDisplayString() == "Lwx.Builders.MicroService.Atributtes.ILwxQueueMessage") ||
                                 param.Type.Name == "ILwxQueueMessage";
            result.Add((param.Name, paramType, isQueueMessage));
        }

        return result;
    }

    private void GenerateSourceFiles(
        string name, string ns, string? uriArg, string queueStageLiteral, string uriStageLiteral,
        string? queueProviderTypeName, string? queueConfigSection, int queueReaders,
        string? handlerErrorPolicyTypeName, string? providerErrorPolicyTypeName,
        string? summary, string? description, string? namingException,
        List<(string ParamName, string ParamType, bool IsQueueMessage)> executeParams)
    {
        var shortQueueStage = queueStageLiteral.Contains('.')
            ? string.Join('.', queueStageLiteral.Split('.').Skip(Math.Max(0, queueStageLiteral.Split('.').Length - 2)))
            : queueStageLiteral;
        var shortUriStage = uriStageLiteral.Contains('.')
            ? string.Join('.', uriStageLiteral.Split('.').Skip(Math.Max(0, uriStageLiteral.Split('.').Length - 2)))
            : uriStageLiteral;

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

        // Build DI parameter list for the hosted service
        var diParams = executeParams.Where(p => !p.IsQueueMessage).ToList();
        var diParamsDecl = string.Join(", ", diParams.Select(p => $"{p.ParamType} {p.ParamName}"));
        var diParamsCtorDecl = diParams.Count > 0 
            ? $", {string.Join(", ", diParams.Select(p => $"{p.ParamType} {p.ParamName}"))}" 
            : string.Empty;
        var diParamsFieldAssign = string.Join("\n        ", diParams.Select(p => $"_{p.ParamName} = {p.ParamName};"));
        var diParamsFieldDecl = string.Join("\n    ", diParams.Select(p => $"private readonly {p.ParamType} _{p.ParamName};"));
        // For scoped DI in HandlerWrapper - use local variable names (not fields)
        var diParamsCallArgs = string.Join(", ", executeParams.Select(p => p.IsQueueMessage ? "msg" : p.ParamName));

        // Check if queue stage is active
        var queueActive = !queueStageLiteral.EndsWith(".None", StringComparison.Ordinal);
        var uriActive = !uriStageLiteral.EndsWith(".None", StringComparison.Ordinal) && !string.IsNullOrEmpty(pathPart);

        string configureBuilderMethod;
        string configureAppMethod;

        if (!queueActive && !uriActive)
        {
            configureBuilderMethod = $$"""
public static void Configure(WebApplicationBuilder builder)
{
    // QueueStage={{shortQueueStage}}, UriStage={{shortUriStage}} - Both disabled
}
""";
            configureAppMethod = $$"""
public static void Configure(WebApplication app)
{
    // QueueStage={{shortQueueStage}}, UriStage={{shortUriStage}} - Both disabled
}
""";
        }
        else
        {
            var queueCondExpr = queueStageLiteral.EndsWith(".DevelopmentOnly", StringComparison.Ordinal)
                ? "builder.Environment.IsDevelopment()"
                : "builder.Environment.IsDevelopment() || builder.Environment.IsProduction()";

            var builderBody = new StringBuilder();
            builderBody.AppendLine($"// QueueStage={shortQueueStage}, UriStage={shortUriStage}");
            
            if (queueActive)
            {
                builderBody.AppendLine($$"""
if ({{queueCondExpr}})
{
    // Register descriptor for health/monitoring
    builder.Services.AddSingleton(new LwxMessageEndpointDescriptor
    {
        Name = "{{GeneratorUtils.EscapeForCSharp(name)}}",
        Description = "{{GeneratorUtils.EscapeForCSharp(description ?? string.Empty)}}",
        QueueReaders = {{queueReaders}},
        QueueConfigSection = "{{GeneratorUtils.EscapeForCSharp(queueConfigSection ?? string.Empty)}}",
        QueueProviderType = typeof({{queueProviderTypeName}}),
        HttpUri = {{(string.IsNullOrEmpty(pathPart) ? "null" : $"\"{GeneratorUtils.EscapeForCSharp(pathPart)}\"")}},
        QueueActive = true,
        HttpActive = {{(uriActive ? "true" : "false")}}
    });

    // Register the hosted service that manages the message endpoint
    builder.Services.AddHostedService<{{name}}HostedService>();
}
""");
            }

            configureBuilderMethod = $$"""
public static void Configure(WebApplicationBuilder builder)
{
{{builderBody.ToString().FixIndent(1, indentFirstLine: false)}}
}
""";

            // App configuration for HTTP endpoint
            var appBody = new StringBuilder();
            appBody.AppendLine($"// QueueStage={shortQueueStage}, UriStage={shortUriStage}");

            if (uriActive)
            {
                var uriCondExpr = uriStageLiteral.EndsWith(".DevelopmentOnly", StringComparison.Ordinal)
                    ? "app.Environment.IsDevelopment()"
                    : "app.Environment.IsDevelopment() || app.Environment.IsProduction()";

                var mapMethod = (httpVerb ?? "POST") switch
                {
                    "GET" => "MapGet",
                    "POST" => "MapPost",
                    "PUT" => "MapPut",
                    "DELETE" => "MapDelete",
                    _ => "MapPost"
                };

                // Build HTTP handler with DI parameters
                var httpDiParams = diParams.Select(p => $"{p.ParamType} {p.ParamName}").ToList();
                httpDiParams.Insert(0, "HttpContext ctx");
                var httpParamsList = string.Join(", ", httpDiParams);
                
                var httpCallArgs = executeParams.Select(p => p.IsQueueMessage ? "msg" : p.ParamName).ToList();
                var httpCallArgsList = string.Join(", ", httpCallArgs);

                appBody.AppendLine($$"""
if ({{uriCondExpr}})
{
    // HTTP endpoint for testing/direct message injection
    var endpoint = app.{{mapMethod}}("{{pathPart}}", async ({{httpParamsList}}) =>
    {
        using var reader = new System.IO.StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();
        var msg = new LwxHttpQueueMessage(body, ctx.Request.Headers);
        await Execute({{httpCallArgsList}});
        return Results.Ok();
    });
    endpoint.WithName("{{name}}");
    {{(!string.IsNullOrEmpty(summary) ? $"endpoint.WithDisplayName(\"{GeneratorUtils.EscapeForCSharp(summary)}\");" : string.Empty)}}
}
""");
            }

            configureAppMethod = $$"""
public static void Configure(WebApplication app)
{
{{appBody.ToString().FixIndent(1, indentFirstLine: false)}}
}
""";
        }

        // Generate the hosted service class
        var hostedServiceClass = string.Empty;
        if (queueActive)
        {
            hostedServiceClass = $$"""

/// <summary>
/// Hosted service that manages the message endpoint queue consumer lifecycle.
/// </summary>
public sealed class {{name}}HostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<{{name}}HostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ILwxQueueProvider? _provider;

    public {{name}}HostedService(IConfiguration configuration, ILogger<{{name}}HostedService> logger, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _provider = new {{queueProviderTypeName}}();
        var errorPolicy = new {{effectiveHandlerErrorPolicy}}();
        var providerErrorPolicy = new {{effectiveProviderErrorPolicy}}();

        _provider.Configure(_configuration, "{{GeneratorUtils.EscapeForCSharp(queueConfigSection ?? string.Empty)}}");
        _provider.SetProviderErrorPolicy(providerErrorPolicy);

        _logger.LogInformation(
            "Starting message endpoint {Name} with section {Section} and concurrency {Concurrency}",
            _provider.Name,
            "{{GeneratorUtils.EscapeForCSharp(queueConfigSection ?? string.Empty)}}",
            {{queueReaders}});

        async Task HandlerWrapper(ILwxQueueMessage msg, CancellationToken ct)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
{{GenerateDiResolution(diParams).FixIndent(4)}}
                await {{name}}.Execute({{diParamsCallArgs}});
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
            _logger.LogInformation("Stopping message endpoint {Name}", _provider.Name);
            await _provider.StopAsync(cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }
}
""";
        }

        // Generate helper class for HTTP message wrapping
        var httpMessageClass = string.Empty;
        if (uriActive)
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
    {{configureBuilderMethod.FixIndent(1, indentFirstLine: false)}}

    {{configureAppMethod.FixIndent(1, indentFirstLine: false)}}
}
{{hostedServiceClass}}
{{httpMessageClass}}
""";

        var generatedFileName = !string.IsNullOrEmpty(namingException)
            ? $"{ns}.{name}.g.cs"
            : $"LwxMessageEndpoint_{name}.g.cs";

        ProcessorUtils.AddGeneratedFile(ctx, generatedFileName, source);
    }

    private static string GenerateDiResolution(List<(string ParamName, string ParamType, bool IsQueueMessage)> diParams)
    {
        if (diParams.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var p in diParams)
        {
            sb.AppendLine($"var {p.ParamName} = scope.ServiceProvider.GetRequiredService<{p.ParamType}>();");
        }
        return sb.ToString().TrimEnd();
    }
}
