using System;
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService.Processors;

namespace Lwx.Builders.MicroService;

// FoundAttribute and LwxConstants moved into the Processors namespace so
// processors and helper classes can reference them from `Lwx.Builders.MicroService.Processors`.

[Generator(LanguageNames.CSharp)]
public class Generator : IIncrementalGenerator
{

        // LwxConstants and FoundAttribute types have been moved into
        // the `Lwx.Builders.MicroService.Processors` namespace (see Processors/Common.cs)
        // so this generator now relies on that shared implementation.

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generate attribute definitions so consumer projects don't need to declare them.
        context.RegisterPostInitializationOutput(ctx =>
        {
            // Use each processor's GenerateAttribute method
            new Processors.LwxEndpointPostInitializationProcessor(ctx).Execute();
            new Processors.LwxWorkerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxServiceBusConsumerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxEventHubConsumerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxTimerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxServiceBusProducerPostInitializationProcessor(ctx).Execute();
            new Processors.LwxEndpointMetadataPostInitializationProcessor(ctx).Execute();
            new Processors.LwxEndpointExtensionsPostInitializationProcessor(ctx).Execute();
            new Processors.LwxServiceConfigPostInitializationProcessor(ctx).Execute();
        });

        // Find attributes whose simple name matches one of the attribute names in our list
        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPotentialAttribute(node),
                transform: static (ctx, ct) => Transform(ctx))
            .Where(x => x is not null);

        // Find classes named ServiceConfig
        var serviceConfigProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => node is ClassDeclarationSyntax cds && cds.Identifier.Text == "ServiceConfig",
                transform: static (ctx, ct) => ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node) as INamedTypeSymbol)
            .Where(x => x is not null);

        // Collect all found attributes and process them together
        context.RegisterSourceOutput(context.CompilationProvider.Combine(attrProvider.Collect()), (spc, tuple) =>
        {
            var compilation = tuple.Left;
            var found = tuple.Right;
            var generateMain = false;
            var serviceConfigLocation = Location.None;
            INamedTypeSymbol? serviceConfigSymbol = null;
            var endpointNames = new List<string>();
            foreach (var f in found)
            {
                if (f == null) continue;
                if (f.AttributeName == LwxConstants.LwxEndpoint)
                {
                    new Processors.LwxEndpointTypeProcessor(f, spc, compilation).Execute();
                    var fullName = f.TargetSymbol.ToDisplayString();
                    var asmName = compilation.AssemblyName ?? string.Empty;
                    if (!string.IsNullOrEmpty(asmName) && fullName.StartsWith(asmName + ".", StringComparison.Ordinal))
                    {
                        fullName = fullName.Substring(asmName.Length + 1);
                    }
                    endpointNames.Add(fullName);
                }
                else if (f.AttributeName == LwxConstants.LwxWorker)
                {
                    new Processors.LwxWorkerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxServiceBusConsumer)
                {
                    new Processors.LwxServiceBusConsumerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxEventHubConsumer)
                {
                    new Processors.LwxEventHubConsumerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxTimer)
                {
                    new Processors.LwxTimerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxServiceBusProducer)
                {
                    new Processors.LwxServiceBusProducerTypeProcessor(f, spc, compilation).Execute();
                }
                else if (f.AttributeName == LwxConstants.LwxServiceConfig)
                {
                    new Processors.LwxServiceConfigTypeProcessor(f, spc, compilation).Execute();
                    serviceConfigLocation = f.Location;
                    // capture the class symbol for the ServiceConfig so we can generate Main in the same namespace
                    serviceConfigSymbol = f.TargetSymbol as INamedTypeSymbol;
                    var attrData = f.AttributeData;
                    if (attrData != null)
                    {
                        var m = attrData.NamedArguments.FirstOrDefault(kv => kv.Key == "GenerateMain");
                        if (!m.Equals(default(KeyValuePair<string, TypedConstant>)) && m.Value.Value is bool b)
                        {
                            generateMain = b;
                        }
                    }
                }
            }

            var swaggerAttr = found.FirstOrDefault(f => f != null && f.AttributeName == LwxConstants.LwxServiceConfig)?.AttributeData;
            if (swaggerAttr == null)
            {
                // If no ServiceConfig attribute is present, emit an error diagnostic requiring it.
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX011",
                        "Missing ServiceConfig",
                        "Projects using Lwx generator must declare a [LwxServiceConfig] class (ServiceConfig.cs) with service metadata.",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
            }
            var fullNs = found.FirstOrDefault()?.TargetSymbol?.ContainingNamespace?.ToDisplayString() ?? "Generated";
            var ns = fullNs.Contains('.') ? fullNs.Substring(0, fullNs.LastIndexOf('.')) : fullNs;
            GenerateLwxConfigure(spc, swaggerAttr, ns);

            if (generateMain)
            {
                // Check if user has their own Program.cs
                var hasProgramCs = compilation.SyntaxTrees.Any(st =>
                    string.Equals(System.IO.Path.GetFileName(st.FilePath), "Program.cs", StringComparison.OrdinalIgnoreCase));
                if (hasProgramCs)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "LWX013",
                            "Program.cs not allowed when GenerateMain is true",
                            "When [LwxServiceConfig(GenerateMain = true)] is set, you must not have a custom Program.cs file. The Program.cs is auto-generated with standard ASP.NET Core setup including LwxConfigure calls and endpoint mapping. Use ServiceConfig.Configure(WebApplicationBuilder) and ServiceConfig.Configure(WebApplication) for additional customizations.",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        serviceConfigLocation));
                }
                else
                {
                    // prefer to place Main inside the ServiceConfig's namespace when available
                    var svcNs = serviceConfigSymbol?.ContainingNamespace?.ToDisplayString() ?? fullNs;
                    var generatedSource = GenerateProgramCs(spc, svcNs, endpointNames);

                    // Emit an informational diagnostic anchored to the attribute location
                    // to help users jump to or inspect the generated ServiceConfig.Main.g.cs
                    var generatedName = "ServiceConfig.Main.g.cs";
                    // attach the whole generated Main method/source in the diagnostic message so IDEs show it as hover text
                    var methodPreview = generatedSource;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "LWX016",
                            "Generated ServiceConfig partial",
                            "Generator produced '{0}' in namespace '{1}'. Generated source:\n{2}",
                            "Generation",
                            DiagnosticSeverity.Info,
                            isEnabledByDefault: true),
                        serviceConfigLocation, generatedName, svcNs, methodPreview));
                }
            }
        });

        // Validate Configure methods in all ServiceConfig classes
        context.RegisterSourceOutput(context.CompilationProvider.Combine(serviceConfigProvider.Collect()), (spc, tuple) =>
        {
            var compilation = tuple.Left;
            var serviceConfigs = tuple.Right;
            foreach (var sc in serviceConfigs)
            {
                if (sc == null) continue;
                ValidateServiceConfigConfigureMethods(sc, spc, compilation);
            }
        });
    }

    private static void GenerateLwxConfigure(SourceProductionContext spc, AttributeData? swaggerAttr, string ns)
    {
        string title = "API";
        string description = "API Description";
        string version = "v1";
        string publishLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";

        if (swaggerAttr != null)
        {
            var m1 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Title");
            if (!m1.Equals(default(KeyValuePair<string, TypedConstant>)) && m1.Value.Value is string s1)
            {
                title = s1;
            }
            var m2 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Description");
            if (!m2.Equals(default(KeyValuePair<string, TypedConstant>)) && m2.Value.Value is string s2)
            {
                description = s2;
            }
            var m3 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Version");
            if (!m3.Equals(default(KeyValuePair<string, TypedConstant>)) && m3.Value.Value is string s3)
            {
                version = s3;
            }

            var m4 = swaggerAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "PublishSwagger");
            if (!m4.Equals(default(KeyValuePair<string, TypedConstant>)) && m4.Value.Value != null)
            {
                var raw = m4.Value.Value;
                if (raw is int iv)
                {
                    publishLiteral = iv switch
                    {
                        1 => "Lwx.Builders.MicroService.Atributes.LwxStage.Development",
                        2 => "Lwx.Builders.MicroService.Atributes.LwxStage.Production",
                        _ => "Lwx.Builders.MicroService.Atributes.LwxStage.None"
                    };
                }
                else
                {
                    var tmp = m4.Value.Value.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributes.LwxStage." + tmp);
                }
            }
        }

        var hasSwagger = swaggerAttr != null && publishLiteral != "Lwx.Builders.MicroService.Atributes.LwxStage.None";

        // Escape values that will be embedded into generated C# string literals.
        static string EscapeForCSharp(string s)
        {
            if (s == null) return string.Empty;
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\\n")
                .Replace("\r", "\\n")
                .Replace("\n", "\\n");
        }

        var escTitle = EscapeForCSharp(title);
        var escDescription = EscapeForCSharp(description);
        var escVersion = EscapeForCSharp(version);
        string swaggerServicesCode;

        var srcEnvContition = 
            publishLiteral.EndsWith(".Development", StringComparison.Ordinal)
                ? "IsDevelopment()"
                : "IsDevelopment() || IsProduction()";

        if (!hasSwagger)
        {
            swaggerServicesCode = string.Empty;
        }
        else
        {
            // Use a single raw template; we only vary the condition expression when embedding.
            swaggerServicesCode = $$"""
                if (builder.Environment.{{srcEnvContition}})
                {
                    builder.Services.AddSwaggerGen(options =>
                    {
                        options.SwaggerDoc("{{escVersion}}", new Microsoft.OpenApi.Models.OpenApiInfo
                        {
                            Title = "{{escTitle}}",
                            Description = "{{escDescription}}",
                            Version = "{{escVersion}}"
                        });
                    });
                }
                """;
        }
            
        string swaggerAppCode;
        if (!hasSwagger)
        {
            swaggerAppCode = string.Empty;
        }
        else
        {
            // Reuse the same envCondition but with the `app.Environment` prefix
            swaggerAppCode = $$"""
                if (app.Environment.{{srcEnvContition}})
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(options =>
                    {
                        options.DocumentTitle = "{{escTitle}}";
                    });
                }
                """;
        }

        var source = $$"""
            // <auto-generated/>
            using System;
            using System.Linq;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            using Lwx.Builders.MicroService.Atributes;

            namespace Lwx.Builders.MicroService.Atributes
            {
                /// <summary>
                /// Provides extension methods for configuring Lwx in ASP.NET Core applications.
                /// </summary>
                public static class LwxEndpointExtensions
                {
                    /// <summary>
                    /// Configures Lwx services, including Swagger if enabled.
                    /// </summary>
                    /// <param name="builder">The web application builder.</param>
                    public static void LwxConfigure(this WebApplicationBuilder builder)
                    {
                        {{swaggerServicesCode.FixIndent(6)}}
                    }

                    /// <summary>
                    /// Configures Lwx app and registers validation to ensure all registered endpoints are mapped through the Lwx mechanism.
                    /// </summary>
                    /// <param name="app">The web application instance.</param>
                    public static void LwxConfigure(this WebApplication app)
                    {
                        {{swaggerAppCode.FixIndent(6)}}

                        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
                        lifetime.ApplicationStarted.Register(() =>
                        {
                            ValidateLwxEndpoints(app);
                        });
                    }

                    private static void ValidateLwxEndpoints(WebApplication app)
                    {
                        var exceptions = new[] { "health", "ready", "swagger" };
                        var endpointDataSources = app.Services.GetServices<EndpointDataSource>();
                        foreach (var dataSource in endpointDataSources)
                        {
                            foreach (var endpoint in dataSource.Endpoints)
                            {
                                if (endpoint.Metadata.GetMetadata<LwxEndpointMetadata>() == null)
                                {
                                    var name = endpoint.DisplayName ?? endpoint.ToString();
                                    if (!exceptions.Any(e => name.Contains(e, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        throw new InvalidOperationException($"Endpoint {name} is not mapped by Lwx mechanism");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """;

        spc.AddSource("LwxEndpointExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateProgramCs(SourceProductionContext spc, string ns, List<string> endpointNames)
    {
        var srcEndpointCalls = new StringBuilder();
        if (endpointNames != null)
        {
            foreach (var endpointName in endpointNames)
            {
                srcEndpointCalls.Append($$"""
                    {{endpointName}}.Configure(app);

                    """);
            }
        }

        var source = $$"""
            // <auto-generated/>
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.Hosting;
            using Lwx.Builders.MicroService.Atributes;

            namespace {{ns}}
            {
                public static partial class ServiceConfig
                {
                    public static void Main(string[] args)
                    {
                        var builder = WebApplication.CreateBuilder(args);

                        // Configure Lwx services, including Swagger if enabled
                        builder.LwxConfigure();

                        // Allow additional configuration in user ServiceConfig.Configure(WebApplicationBuilder)
                        Configure(builder);

                        var app = builder.Build();

                        // Configure Lwx app, including Swagger UI if enabled
                        app.LwxConfigure();

                        // Allow additional configuration in user ServiceConfig.Configure(WebApplication)
                        Configure(app);

                        // Map all endpoints generated by Lwx source generator
                        {{srcEndpointCalls.FixIndent(3)}}

                        app.Run();
                    }
                }
            }
            """;

        spc.AddSource("ServiceConfig.Main.g.cs", SourceText.From(source, Encoding.UTF8));

        // return the generated source so callers may reference it (for diagnostics, tests, etc.)
        return source;
    }

    private static FoundAttribute? Transform(GeneratorSyntaxContext ctx)
    {
        var attributeSyntax = (AttributeSyntax)ctx.Node;

        // Use the semantic model to determine the attribute type correctly
        var info = ctx.SemanticModel.GetSymbolInfo(attributeSyntax, CancellationToken.None);
        var attrType = info.Symbol switch
        {
            IMethodSymbol ms => ms.ContainingType,
            INamedTypeSymbol nts => nts,
            _ => null
        };

        if (attrType == null) return default(FoundAttribute);

        var attrName = attrType.Name;
        if (attrName.EndsWith("Attribute")) attrName = attrName.Substring(0, attrName.Length - "Attribute".Length);
        if (!LwxConstants.AttributeNames.Contains(attrName, StringComparer.Ordinal)) return default(FoundAttribute);

        var parent = attributeSyntax.Parent?.Parent;
        if (parent == null) return default(FoundAttribute);

        var declaredSymbol = ctx.SemanticModel.GetDeclaredSymbol(parent, CancellationToken.None);
        if (declaredSymbol == null) return default(FoundAttribute);

        // Find the corresponding AttributeData for the declared symbol (if any)
        var attrData = declaredSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass != null && ad.AttributeClass.ToDisplayString() == attrType.ToDisplayString());

        return new FoundAttribute(attrName, declaredSymbol, parent.GetLocation(), attrData);
    }

    private static bool IsPotentialAttribute(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute) return false;
        // Name may be IdentifierName or QualifiedName (e.g. Namespace.LwxEndpoint)
        var name = attribute.Name.ToString();
        var simple = name.Contains('.') ? name.Substring(name.LastIndexOf('.') + 1) : name;
        if (simple.EndsWith("Attribute")) simple = simple.Substring(0, simple.Length - "Attribute".Length);
        return LwxConstants.AttributeNames.Contains(simple, StringComparer.Ordinal);
    }

    private static void ValidateServiceConfigConfigureMethods(INamedTypeSymbol typeSymbol, SourceProductionContext spc, Compilation compilation)
    {
        // Resolve expected parameter types
        var webAppBuilderType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplicationBuilder");
        var webAppType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplication");

        foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.IsImplicitlyDeclared) continue;
            // Only inspect ordinary methods (skip constructors, accessors, etc.)
            if (member.MethodKind != MethodKind.Ordinary) continue;

            // Only consider declared public methods
            if (member.DeclaredAccessibility == Accessibility.Public)
            {
                // Allowed public methods are:
                // public static void Configure(WebApplicationBuilder)
                // public static void Configure(WebApplication)
                if (member.Name == "Configure")
                {
                    var valid = member.IsStatic && member.Parameters.Length == 1 && member.ReturnsVoid;
                    var param = member.Parameters.Length == 1 ? member.Parameters[0].Type : null;
                    var paramMatches = param != null && (SymbolEqualityComparer.Default.Equals(param, webAppBuilderType) || SymbolEqualityComparer.Default.Equals(param, webAppType));
                    if (!valid || !paramMatches)
                    {
                        var descriptor = new DiagnosticDescriptor(
                            "LWX014",
                            "Invalid ServiceConfig.Configure signature",
                            "ServiceConfig.Configure must be declared as a public static void Configure(WebApplicationBuilder) or public static void Configure(WebApplication). Found: '{0}'",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true);

                        var locMember = member.Locations.FirstOrDefault() ?? Location.None;
                        spc.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.ToDisplayString()));
                    }
                }
                else
                {
                    // Any other public method is disallowed
                    var descriptor = new DiagnosticDescriptor(
                        "LWX015",
                        "Unexpected public method in ServiceConfig",
                        "Public method '{0}' is not allowed in ServiceConfig. Only public static Configure(WebApplicationBuilder) and Configure(WebApplication) are permitted for customization when using the generator.",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);

                    var locMember = member.Locations.FirstOrDefault() ?? Location.None;
                    spc.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.Name));
                }
            }
        }
    }
}

// Helper extension to fix indentation when embedding generated multi-line text
internal static class LwxGeneratedStringExtensions
{
    public static string FixIndent(this StringBuilder source, int indentLevels)
    {
        if (source == null) return string.Empty;
        return source.ToString().FixIndent(indentLevels);
    }

    /// <summary>
    /// Fix the indentation of a multi-line snippet. The parameter <paramref name="indentLevels"/>
    /// is treated as indentation levels and expanded to spaces using 4 spaces per level.
    /// For example, FixIndent(1) prefixes each non-empty line with 4 spaces.
    /// </summary>
    public static string FixIndent(this string source, int indentLevels)
    {
        if (string.IsNullOrEmpty(source)) return source;
        if (indentLevels <= 0) return source;
        // Treat indentLevels as the number of indentation units (4 spaces each)
        var spacesPerLevel = 4;
        var indent = indentLevels * spacesPerLevel;
        var indentStr = new string(' ', indent);
        // Normalize line endings to \n for predictable behavior
        var normalized = source.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
            {
                lines[i] = indentStr + lines[i];
            }
        }
        return string.Join("\n", lines);
    }
}
