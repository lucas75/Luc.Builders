using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Linq;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxServiceConfigTypeProcessor(
    Generator parent,
    Compilation compilation,
    SourceProductionContext ctx,
    AttributeInstance attr
)
{
    public void Execute()
    {
        // enforce file path and namespace matching for service config marker classes
        ProcessorUtils.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);
        // Ensure the service attribute is only used in a file explicitly named Service.cs
        var loc = attr.TargetSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc != null)
        {
            var fp = loc.SourceTree?.FilePath;
            if (!string.IsNullOrEmpty(fp))
            {
                var fileName = System.IO.Path.GetFileName(fp);
                if (!string.Equals(fileName, "Service.cs", StringComparison.OrdinalIgnoreCase))
                {
                    var descriptor = new DiagnosticDescriptor(
                        "LWX012",
                        "Service must be declared in Service.cs",
                        "[LwxServiceConfig] must be declared in a file named 'Service.cs'. Found in '{0}'",
                        "Configuration",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true);

                    ctx.ReportDiagnostic(Diagnostic.Create(descriptor, loc, fileName));
                    return; // bail out - incorrect usage
                }
            }
        }

        var name = ProcessorUtils.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";

        string? title = null;
        string? description = null;
        string? version = null;
        string publishLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";
        // generator no longer creates a Main entrypoint; instead we emit helpers on the consumer `Service` type

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

            // GenerateMain option removed — do not interpret this flag
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

        // Validate Service methods signatures and public surface
        var typeSymbol = attr.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol != null)
        {
            // Validate Configure methods declared on the Service type
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
                                "Invalid Service.Configure signature",
                                "Service.Configure must be declared as a public static void Configure(WebApplicationBuilder) or public static void Configure(WebApplication). Found: '{0}'",
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
                            "Unexpected public method in Service",
                            "Public method '{0}' is not allowed in Service. Only public static Configure(WebApplicationBuilder) and Configure(WebApplication) are permitted for customization when using the generator.",
                            "Configuration",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true);

                        var locMember = member.Locations.FirstOrDefault() ?? Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(descriptor, locMember, member.Name));
                    }
                }
            }
        }

        // Determine the service/app wiring snippets (previously generated in a separate
        // LwxEndpointExtensions.g.cs). We now inline them into the Service helper file
        // so everything the consumer needs is available on the Service partial.
        var srcServices = string.Empty;
        var srcApp = string.Empty;
        if (publishLiteral != "Lwx.Builders.MicroService.Atributes.LwxStage.None")
        {
            var srcEnvCondition = publishLiteral.EndsWith(".Development", StringComparison.Ordinal)
                ? "app.Environment.IsDevelopment()"
                : "app.Environment.IsDevelopment() || app.Environment.IsProduction()";

            srcServices = "builder.Services.AddSwaggerGen();";
            srcApp = $"if ({srcEnvCondition})\n{{\n    app.UseSwagger();\n    app.UseSwaggerUI();\n}}\n";
        }

        // Always generate Service helpers for application wiring (inline endpoint wiring)
        GenerateServiceHelpers(srcServices, srcApp);
    }

    /// <summary>
    /// Generates the LwxEndpointExtensions.g.cs content which wires Swagger/OpenAPI services and app middleware
    /// based on the ServiceConfig attribute metadata.
    /// </summary>
    // Endpoint extensions were previously emitted into a separate file. We no longer
    // generate LwxEndpointExtensions; endpoint + swagger wiring is now inlined into
    // the generated Service helpers. This keeps generated wiring centralized and
    // avoids consumers needing to import additional extension types.
    // (Method intentionally removed.)
    

    /// <summary>
    /// Generate a helper Service partial in the consumer namespace which exposes
    /// Configure(WebApplicationBuilder) and Configure(WebApplication) methods that
    /// wire Lwx services and endpoints into an application. This replaces the previous
    /// generated Main entrypoint so consumer projects may provide their own Program.cs
    /// and call these strongly-named helpers from tests or application code.
    /// </summary>
    private void GenerateServiceHelpers(string srcServices, string srcApp)
    {
        // Generate the helpers in the same namespace that contains the consumer Service type
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? (compilation.AssemblyName ?? "Generated");

        var srcWorkerCalls = new StringBuilder();
        foreach (var workerName in parent.WorkerNames)
        {
            srcWorkerCalls.Append($"{workerName}.Configure(builder);\n\n");
        }

        var srcEndpointCalls = new StringBuilder();
        foreach (var endpointName in parent.EndpointNames)
        {
            srcEndpointCalls.Append($"{endpointName}.Configure(app);\n\n");
        }

        // Resolve the fully-qualified Service type name so we can call user-provided Configure overloads.
        var serviceConfigTypeName = ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation);

        // Detect whether user ServiceConfig declared Configure(WebApplicationBuilder) and/or Configure(WebApplication)
        var webAppBuilderType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplicationBuilder");
        var webAppType = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.WebApplication");

        bool hasBuilderConfigure = false;
        bool hasAppConfigure = false;
        if (attr.TargetSymbol is INamedTypeSymbol tSym)
        {
            foreach (var m in tSym.GetMembers().OfType<IMethodSymbol>())
            {
                if (m.Name != "Configure" || !m.IsStatic || m.DeclaredAccessibility != Accessibility.Public) continue;
                if (m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, webAppBuilderType)) hasBuilderConfigure = true;
                if (m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, webAppType)) hasAppConfigure = true;
            }
        }

        // Choose generated method names. If the consumer already declares Configure(WebApplicationBuilder)
        // or Configure(WebApplication) we must avoid emitting methods with those signatures to prevent
        // duplicate definition errors. In that case we keep the LwxConfigure name for generator-created
        // helpers. Otherwise we emit the nicer `Configure` helpers so consumers can call `Service.Configure(...)`
        // directly from Program.cs.
        var builderMethodName = hasBuilderConfigure ? "LwxConfigure" : "Configure";
        var appMethodName = hasAppConfigure ? "LwxConfigure" : "Configure";

        var source = $$"""
            // <auto-generated/>
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.Hosting;
            using Microsoft.Extensions.DependencyInjection;
            using Lwx.Builders.MicroService.Atributes;

            namespace {{ns}};

            public static partial class Service
            {
                /// <summary>
                /// Configures Lwx services on the provided WebApplicationBuilder and invokes any
                /// consumer-provided Service.Configure(WebApplicationBuilder) if present.
                /// </summary>
                public static void {{builderMethodName}}(WebApplicationBuilder builder)
                {
                    // Generator-level service wiring (e.g. swagger or other services)
                    {{srcServices}}

                    // Register Lwx workers
                    {{srcWorkerCalls.FixIndent(2, indentFirstLine: false)}}

                    // Allow user customization on Service.Configure(WebApplicationBuilder)
                    {{(hasBuilderConfigure ? (serviceConfigTypeName + ".Configure(builder);") : string.Empty)}}
                }

                /// <summary>
                /// Configures the application (middleware, endpoints) and invokes any
                /// consumer-provided Service.Configure(WebApplication) if present.
                /// </summary>
                public static void {{appMethodName}}(WebApplication app)
                {
                    // Generator-level app wiring (e.g. swagger UI)
                    {{srcApp}}

                    // Allow user customization on Service.Configure(WebApplication)
                    {{(hasAppConfigure ? (serviceConfigTypeName + ".Configure(app);") : string.Empty)}}

                    // Map endpoints generated by Lwx
                    {{srcEndpointCalls.FixIndent(2, indentFirstLine: false)}}
                }
            }
            """;

        var genName = ProcessorUtils.SafeIdentifier(ns) + ".Service.g.cs";
        ctx.AddSource(genName, SourceText.From(source, System.Text.Encoding.UTF8));

        // Emit informational diagnostic with the generated source for IDE preview
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX016",
            "Generated Service partial",
            "Generator produced '{0}' in namespace '{1}'. Generated source:\n{2}",
                "Generation",
                DiagnosticSeverity.Info,
                isEnabledByDefault: true),
            attr.Location, genName, ns, source));
    }

    /// <summary>
    /// Report that no Service descriptor was found in the compilation.
    /// </summary>
    public static void ReportMissingServiceConfig(SourceProductionContext spc)
    {
        spc.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX011",
                "Missing Service",
                "Projects using the Lwx generator must declare a [LwxServiceConfig] on a type named 'Service' in a file named Service.cs with service metadata.",
                "Configuration",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            Location.None));
    }

    /// <summary>
    /// Report a duplicate Service attribute occurrence.
    /// </summary>
    public static void ReportMultipleServiceConfig(SourceProductionContext spc, Location location)
    {
        spc.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX017",
                "Multiple Service declarations",
                "Multiple [LwxServiceConfig] declarations found. Only one [LwxServiceConfig] is allowed per project and it must be declared on a type named 'Service'.",
                "Configuration",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            location));
    }
}
