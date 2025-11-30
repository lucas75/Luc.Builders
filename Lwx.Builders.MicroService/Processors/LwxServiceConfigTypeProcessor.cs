using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Linq;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxServiceConfigTypeProcessor(
    FoundAttribute attr,
    SourceProductionContext ctx,
    Compilation compilation,
    Generator parent
)
{
    public void Execute()
    {
        // enforce file path and namespace matching for service config marker classes
        GeneratorHelpers.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);
        // Ensure the service config attribute is only used in a file explicitly named ServiceConfig.cs
        var loc = attr.TargetSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc != null)
        {
            var fp = loc.SourceTree?.FilePath;
            if (!string.IsNullOrEmpty(fp))
            {
                var fileName = System.IO.Path.GetFileName(fp);
                if (!string.Equals(fileName, "ServiceConfig.cs", StringComparison.OrdinalIgnoreCase))
                {
                    var descriptor = new DiagnosticDescriptor(
                        "LWX012",
                        "ServiceConfig must be declared in ServiceConfig.cs",
                        "[LwxServiceConfig] must be declared in a file named 'ServiceConfig.cs'. Found in '{0}'",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);

                    ctx.ReportDiagnostic(Diagnostic.Create(descriptor, loc, fileName));
                    return; // bail out - incorrect usage
                }
            }
        }

        var name = GeneratorHelpers.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        string? title = null;
        string? description = null;
        string? version = null;
        string publishLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";
        bool generateMain = false;

        if (attr.AttributeData != null)
        {
            var named = attr.AttributeData.ToNamedArgumentMap();
            if (named.TryGetValue("Title", out var t) && t.Value is string s1)
            {
                title = s1;
            }

            if (named.TryGetValue("Description", out var d) && d.Value is string s2)
            {
                description = s2;
            }

            if (named.TryGetValue("Version", out var v) && v.Value is string s3)
            {
                version = s3;
            }

            if (named.TryGetValue("PublishSwagger", out var p) && p.Value != null)
            {
                var raw = p.Value;
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
                    var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributes.LwxStage." + tmp);
                }
            }

            if (named.TryGetValue("GenerateMain", out var gm) && gm.Value is bool b)
            {
                generateMain = b;
            }
        }

        // If the publish stage is active (not None) make sure Swashbuckle is available
        if (publishLiteral != "Lwx.Builders.MicroService.Atributes.LwxStage.None")
        {
            var openApiInfoType = compilation.GetTypeByMetadataName("Microsoft.OpenApi.Models.OpenApiInfo");
            if (openApiInfoType == null)
            {
                // Emit a diagnostic warning/error so users know they requested swagger publishing but
                // don't have the Swashbuckle package installed. Do not abort generation so generated
                // endpoint extension helpers are still emitted for inspection/testing.
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "LWX003",
                        "Missing Swashbuckle package",
                        "LwxServiceConfig requests PublishSwagger but the Swashbuckle.AspNetCore package is missing. Install it using: dotnet add package Swashbuckle.AspNetCore",
                        "Dependencies",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    attr.Location));
            }
        }

        // Validate ServiceConfig methods signatures and public surface
        var typeSymbol = attr.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol != null)
        {
            // Validate Configure methods declared on the ServiceConfig class
            // Expected allowed public static methods:
            // - public static void Configure(WebApplicationBuilder)
            // - public static void Configure(WebApplication)
            var webAppBuilderType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplicationBuilder");
            var webAppType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplication");

            foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.IsImplicitlyDeclared) continue;
                if (member.MethodKind != MethodKind.Ordinary) continue;

                if (member.DeclaredAccessibility == Accessibility.Public)
                {
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
                            ctx.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.ToDisplayString()));
                        }
                    }
                    else
                    {
                        var descriptor = new DiagnosticDescriptor(
                            "LWX015",
                            "Unexpected public method in ServiceConfig",
                            "Public method '{0}' is not allowed in ServiceConfig. Only public static Configure(WebApplicationBuilder) and Configure(WebApplication) are permitted for customization when using the generator.",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true);

                        var locMember = member.Locations.FirstOrDefault() ?? Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.Name));
                    }
                }
            }
        }

        GenerateLwxEndpointExtensions();

        if (generateMain)
        {            
            GenerateMain();
        }
    }

    /// <summary>
    /// Generates the LwxEndpointExtensions.g.cs content which wires Swagger/OpenAPI services and app middleware
    /// based on the ServiceConfig attribute metadata.
    /// </summary>
    private void GenerateLwxEndpointExtensions()
    {
        // Use the ServiceConfig attribute data from the instance
        var swaggerAttr = attr.AttributeData;

        string title = "API";
        string description = "API Description";
        string version = "v1";
        string publishLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";

        if (swaggerAttr != null)
        {
            var named = swaggerAttr.ToNamedArgumentMap();
            if (named.TryGetValue("Title", out var t) && t.Value is string s1)
            {
                title = s1;
            }

            if (named.TryGetValue("Description", out var d) && d.Value is string s2)
            {
                description = s2;
            }

            if (named.TryGetValue("Version", out var v) && v.Value is string s3)
            {
                version = s3;
            }

            if (named.TryGetValue("PublishSwagger", out var p) && p.Value != null)
            {
                var raw = p.Value;
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
                    var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributes.LwxStage." + tmp);
                }
            }
        }

        var hasSwagger = swaggerAttr != null && publishLiteral != "Lwx.Builders.MicroService.Atributes.LwxStage.None";

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
                        options.SwaggerDoc("{{Util.EscapeForCSharp(version)}}", new Microsoft.OpenApi.Models.OpenApiInfo
                        { 
                            Title = "{{Util.EscapeForCSharp(title)}}",
                            Description = "{{Util.EscapeForCSharp(description)}}",
                            Version = "{{Util.EscapeForCSharp(version)}}"
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
            swaggerAppCode = $$"""
                if (app.Environment.{{srcEnvContition}})
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(options =>
                    {
                        options.DocumentTitle = "{{Util.EscapeForCSharp(title)}}";
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
                        {{swaggerServicesCode.FixIndent(3,indentFirstLine: false)}}
                    }

                    /// <summary>
                    /// Configures Lwx app and registers validation to ensure all registered endpoints are mapped through the Lwx mechanism.
                    /// </summary>
                    /// <param name="app">The web application instance.</param>
                    public static void LwxConfigure(this WebApplication app)
                    {
                        {{swaggerAppCode.FixIndent(3,indentFirstLine: false)}}

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

        ctx.AddSource("LwxEndpointExtensions.g.cs", SourceText.From(source, System.Text.Encoding.UTF8));
    }

    /// <summary>
    /// Report that no ServiceConfig attribute was found in the compilation.
    /// </summary>
    public static void ReportMissingServiceConfig(SourceProductionContext spc)
    {
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

    /// <summary>
    /// Report a duplicate ServiceConfig attribute occurrence.
    /// </summary>
    public static void ReportMultipleServiceConfig(SourceProductionContext spc, Location location)
    {
        spc.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX017",
                "Multiple ServiceConfig declarations",
                "Multiple [LwxServiceConfig] declarations found. Only one [LwxServiceConfig] is allowed per project.",
                "Configuration",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            location));
    }

    /// <summary>
    /// Generate Main (ServiceConfig.Main.g.cs) source that maps endpoints and calls worker configuration.
    /// </summary>
    private void GenerateMain()
    {
        // When generating Main we must validate that the consumer project does not have an existing Program.cs
        // if the generator is expected to produce Main. The generator previously did this in Generator.cs; move
        // the check here so the processor owns all ServiceConfig / Main generation concerns.
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        var hasProgramCs = compilation.SyntaxTrees.Any(st =>
            string.Equals(System.IO.Path.GetFileName(st.FilePath), "Program.cs", StringComparison.OrdinalIgnoreCase));

        if (hasProgramCs)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "LWX013",
                    "Program.cs not allowed when GenerateMain is true",
                    "When [LwxServiceConfig(GenerateMain = true)] is set, you must not have a custom Program.cs file. The Program.cs is auto-generated with standard ASP.NET Core setup including LwxConfigure calls and endpoint mapping. Use ServiceConfig.Configure(WebApplicationBuilder) and ServiceConfig.Configure(WebApplication) for additional customizations.",
                    "Configuration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                attr.Location));

            // Bail out - consumer has a Program.cs and generator must not overwrite it.
            return;
        }

        var srcEndpointCalls = new StringBuilder();
        foreach (var endpointName in parent.EndpointNames)
        {
            srcEndpointCalls.Append($$"""
                {{endpointName}}.Configure(app);

                """);
        }

        var srcWorkerCalls = new StringBuilder();
        foreach (var workerName in parent.WorkerNames)
        {
            srcWorkerCalls.Append($$"""
                {{workerName}}.Configure(builder);

                """);
        }

        var source = $$"""
            // <auto-generated/>
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.Hosting;
            using Lwx.Builders.MicroService.Atributes;

            namespace {{attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated"}};

            public static partial class ServiceConfig
            {
                    public static void Main(string[] args)
                    {
                        var builder = WebApplication.CreateBuilder(args);

                        // Configure Lwx services, including Swagger if enabled
                        builder.LwxConfigure();

                        // Register workers generated by Lwx (each worker may decide whether to run based on stage)
                        {{srcWorkerCalls.FixIndent(2, indentFirstLine: false)}}

                        // Allow additional configuration in user ServiceConfig.Configure(WebApplicationBuilder)
                        Configure(builder);

                        var app = builder.Build();

                        // Configure Lwx app, including Swagger UI if enabled
                        app.LwxConfigure();

                        // Allow additional configuration in user ServiceConfig.Configure(WebApplication)
                        Configure(app);

                        // Map all endpoints generated by Lwx source generator
                        {{srcEndpointCalls.FixIndent(2, indentFirstLine: false)}}

                        app.Run();
                    }
                }
            """;

        ctx.AddSource("ServiceConfig.Main.g.cs", SourceText.From(source, System.Text.Encoding.UTF8));

        // Emit informational diagnostic with the generated source for IDE preview
        var generatedName = "ServiceConfig.Main.g.cs";
        var methodPreview = source;
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX016",
                "Generated ServiceConfig partial",
                "Generator produced '{0}' in namespace '{1}'. Generated source:\n{2}",
                "Generation",
                DiagnosticSeverity.Info,
                isEnabledByDefault: true),
            attr.Location, generatedName, ns, methodPreview));

        // generated source has been added via ctx.AddSource and diagnostics emitted
        return;
    }
}
