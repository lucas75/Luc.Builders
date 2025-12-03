using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Linq;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxServiceTypeProcessor(
    Generator parent,
    Compilation compilation,
    SourceProductionContext ctx,
    AttributeInstance attr
)
{
    public void Execute()
    {
        // enforce file path and namespace matching for service descriptor classes
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
                        "[LwxService] must be declared in a file named 'Service.cs'. Found in '{0}'",
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
        string publishLiteral = "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
        // generator no longer creates a Main entrypoint; instead we emit helpers on the consumer `Service` type

        double readinessPercent = 80.0; // default percent
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
                        1 => "Lwx.Builders.MicroService.Atributtes.LwxStage.DevelopmentOnly",
                        2 => "Lwx.Builders.MicroService.Atributtes.LwxStage.All",
                        _ => "Lwx.Builders.MicroService.Atributtes.LwxStage.None"
                    };
                }
                else
                {
                    var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
                    publishLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributtes.LwxStage." + tmp);
                }
            }

            if (named.TryGetValue("ReadnessMaxPercent", out var r) && r.Value != null)
            {
                var raw = r.Value;
                double val = 0.8;
                if (raw is double dv) val = dv;
                else if (raw is float fv) val = fv;
                else if (raw is int iv) val = iv;
                else if (raw is long lv) val = lv;
                else
                {
                    var s = raw.ToString();
                    if (!double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tmp)) tmp = 0.8;
                    val = tmp;
                }
                // attribute expected as fraction (0..1) — convert to percent
                readinessPercent = val > 1.0 ? val : val * 100.0;
            }

            // GenerateMain option removed — do not interpret this flag
        }

        // If the publish stage is active (not None) make sure Swashbuckle is available
        if (publishLiteral != "Lwx.Builders.MicroService.Atributtes.LwxStage.None")
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
                        "LwxService requests PublishSwagger but the Swashbuckle.AspNetCore package is missing. Install it using: dotnet add package Swashbuckle.AspNetCore",
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
        if (publishLiteral != "Lwx.Builders.MicroService.Atributtes.LwxStage.None")
        {
            var srcEnvCondition = publishLiteral.EndsWith(".DevelopmentOnly", StringComparison.Ordinal)
                ? "app.Environment.IsDevelopment()"
                : "app.Environment.IsDevelopment() || app.Environment.IsProduction()";

            srcServices = "builder.Services.AddSwaggerGen();";
            srcApp = $"if ({srcEnvCondition})\n{{\n    app.UseSwagger();\n    app.UseSwaggerUI();\n}}\n";
        }

        // Always generate Service helpers for application wiring (inline endpoint wiring)
        GenerateServiceHelpers(srcServices, srcApp, readinessPercent);
    }

    private void GenerateServiceHelpers(string srcServices, string srcApp, double readinessDefaultPercent)
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

        // Build console listing snippets for endpoints and workers
        var srcList = new System.Text.StringBuilder();
        var assemblyRoot = compilation.AssemblyName ?? string.Empty;
        foreach (var e in parent.EndpointInfos)
        {
            var displayType = e.TypeName ?? string.Empty;
            if (!string.IsNullOrEmpty(assemblyRoot) && displayType.StartsWith(assemblyRoot + ".", StringComparison.Ordinal))
            {
                displayType = displayType.Substring(assemblyRoot.Length + 1);
            }
            // If the type lives under Endpoints or Workers in the same assembly, strip that segment as well
            if (displayType.StartsWith("Endpoints.", StringComparison.Ordinal)) displayType = displayType.Substring("Endpoints.".Length);
            if (displayType.StartsWith("Workers.", StringComparison.Ordinal)) displayType = displayType.Substring("Workers.".Length);
            var escDisplay = GeneratorUtils.EscapeForCSharp(displayType);
            var escPath = GeneratorUtils.EscapeForCSharp(e.Path ?? string.Empty);
            var escMethod = GeneratorUtils.EscapeForCSharp(e.HttpMethod ?? "GET");
            srcList.Append($"System.Console.WriteLine(\"Endpoint: {escMethod} {escPath} -> {escDisplay}\");\n");
        }

        foreach (var w in parent.WorkerInfos)
        {
            var displayType = w.TypeName ?? string.Empty;
            if (!string.IsNullOrEmpty(assemblyRoot) && displayType.StartsWith(assemblyRoot + ".", StringComparison.Ordinal))
            {
                displayType = displayType.Substring(assemblyRoot.Length + 1);
            }
            if (displayType.StartsWith("Endpoints.", StringComparison.Ordinal)) displayType = displayType.Substring("Endpoints.".Length);
            if (displayType.StartsWith("Workers.", StringComparison.Ordinal)) displayType = displayType.Substring("Workers.".Length);
            var escDisplay = GeneratorUtils.EscapeForCSharp(displayType);
            srcList.Append($"System.Console.WriteLine(\"Worker: {escDisplay} nThreads={w.Threads}\");\n");
        }
        var serviceTypeName = ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation);
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

        // Additional method names for smaller wiring helpers; these avoid collisions
        // with consumer-provided Configure overloads (consumer is only allowed
        // to define Configure(WebApplicationBuilder) and Configure(WebApplication)).
        var builderSwaggerMethod = "ConfigureSwagger";
        var builderWorkersMethod = "ConfigureWorkers";
        var appSwaggerMethod = "ConfigureSwagger";
        var appHealthzMethod = "ConfigureHealthz";
        var appEndpointsMethod = "ConfigureEndpoints";

        var readinessDefaultPercentStr = readinessDefaultPercent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        var source = $$"""
            // <auto-generated/>
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.Hosting;
            using Microsoft.Extensions.DependencyInjection;
            using Lwx.Builders.MicroService.Atributtes;

            namespace {{ns}};

            public static partial class Service
            {
                /// <summary>
                /// Configures Lwx services on the provided WebApplicationBuilder and invokes any
                /// consumer-provided Service.Configure(WebApplicationBuilder) if present.
                /// </summary>
                public static void {{builderMethodName}}(WebApplicationBuilder builder)
                {
                    // Use split configure methods so consumers can call specific parts
                    // as needed and for clearer separation of concerns.
                    {{builderSwaggerMethod}}(builder);
                    {{builderWorkersMethod}}(builder);

                    // Allow user customization on Service.Configure(WebApplicationBuilder)
                    {{(hasBuilderConfigure ? (serviceTypeName + ".Configure(builder);") : string.Empty)}}
                }

                /// <summary>
                /// Configures the application (middleware, endpoints) and invokes any
                /// consumer-provided Service.Configure(WebApplication) if present.
                /// </summary>
                public static void {{appMethodName}}(WebApplication app)
                {
                    // Run split configure steps in a defined order
                    // Print a summary of Lwx resources on application started
                    app.Lifetime.ApplicationStarted.Register(() =>
                    {
                        {{srcList.FixIndent(2, indentFirstLine: false)}}
                    });
                    {{appSwaggerMethod}}(app);
                    {{appHealthzMethod}}(app);
                    {{appEndpointsMethod}}(app);

                    // Allow user customization on Service.Configure(WebApplication)
                    {{(hasAppConfigure ? (serviceTypeName + ".Configure(app);") : string.Empty)}}
                }
                
                /// <summary>
                /// Register Swagger/OpenAPI related services (if enabled)
                /// </summary>
                public static void {{builderSwaggerMethod}}(WebApplicationBuilder builder)
                {
                    // Generator-level service wiring (e.g. swagger or other services)
                    {{srcServices}}
                }

                /// <summary>
                /// Register Lwx workers and related services
                /// </summary>
                public static void {{builderWorkersMethod}}(WebApplicationBuilder builder)
                {
                    // Register Lwx workers
                    {{srcWorkerCalls.FixIndent(2, indentFirstLine: false)}}
                }

                /// <summary>
                /// Configure Swagger/OpenAPI on the app pipeline (if enabled)
                /// </summary>
                public static void {{appSwaggerMethod}}(WebApplication app)
                {
                    // Generator-level app wiring (e.g. swagger UI)
                    {{srcApp}}
                }

                        /// <summary>
                        /// Configure healthz endpoints or middleware (emit /health and /ready endpoints by default)
                        /// </summary>
                        public static void {{appHealthzMethod}}(WebApplication app)
                        {
                            // Implement a TaskCompletionSource that is completed when the application raises ApplicationStarted
                            // This ensures /health returns OK only after startup finished (as requested in TODO)
                            System.Threading.Tasks.TaskCompletionSource<object?>? _lwxStartTcs = new System.Threading.Tasks.TaskCompletionSource<object?>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                            app.Lifetime.ApplicationStarted.Register(() => _lwxStartTcs.TrySetResult(null));

                            app.MapGet("/health", async (HttpContext ctx) =>
                            {
                                await _lwxStartTcs.Task;
                                return Results.Ok(new { status = "healthy" });
                            });

                                app.MapGet("/ready", async (HttpContext ctx) =>
                                {
                                    if (!_lwxStartTcs.Task.IsCompleted) return Results.StatusCode(503);

                                    // Measure CPU load over a short interval to estimate pod load and determine readiness.
                                    // This is a lightweight check and should not be relied upon for precise load-balancing decisions.
                                    try
                                    {
                                        double readinessMaxPercent = {{readinessDefaultPercentStr}};
                                        // Allow override via configuration: Lwx:ReadinessMaxPercent (0..1 or 0..100)
                                        var cfgVal = app.Configuration.GetValue<double?>("Lwx:ReadinessMaxPercent");
                                        if (cfgVal.HasValue)
                                        {
                                            var val = cfgVal.Value;
                                            readinessMaxPercent = val > 1.0 ? val : val * 100.0;
                                        }
                                        var proc = System.Diagnostics.Process.GetCurrentProcess();
                                        var cpuStart = proc.TotalProcessorTime;
                                        var swStart = DateTime.UtcNow;
                                        await System.Threading.Tasks.Task.Delay(200);
                                        var cpuEnd = proc.TotalProcessorTime;
                                        var elapsedMs = (DateTime.UtcNow - swStart).TotalMilliseconds;
                                        var cpuUsedMs = (cpuEnd - cpuStart).TotalMilliseconds;
                                        var cpuPercent = (cpuUsedMs / (elapsedMs * (Environment.ProcessorCount == 0 ? 1 : Environment.ProcessorCount))) * 100.0;
                                        if (cpuPercent >= readinessMaxPercent)
                                        {
                                            return Results.StatusCode(503);
                                        }
                                        return Results.Ok(new { status = "ready", cpu = cpuPercent.ToString("F1"), threshold = readinessMaxPercent.ToString("F1") });
                                    }
                                    catch
                                    {
                                        // If measurement fails for any reason, return ready (fail-open) to avoid blocking rollout.
                                        return Results.Ok(new { status = "ready" });
                                    }
                                });
                        }

                /// <summary>
                /// Wire Lwx-generated endpoints on the app
                /// </summary>
                public static void {{appEndpointsMethod}}(WebApplication app)
                {
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
    public static void ReportMissingService(SourceProductionContext spc)
    {
        spc.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX011",
                "Missing Service",
                "Projects using the Lwx generator must declare a [LwxService] on a type named 'Service' in a file named Service.cs with service metadata.",
                "Configuration",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            Location.None));
    }

    /// <summary>
    /// Report a duplicate Service attribute occurrence.
    /// </summary>
    public static void ReportMultipleService(SourceProductionContext spc, Location location)
    {
        spc.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "LWX017",
                "Multiple Service declarations",
                "Multiple [LwxService] declarations found. Only one [LwxService] is allowed per project and it must be declared on a type named 'Service'.",
                "Configuration",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true),
            location));
    }
}
