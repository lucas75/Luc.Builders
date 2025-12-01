using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxWorkerTypeProcessor(
    Generator parent,
    Compilation compilation,
    SourceProductionContext ctx,
    AttributeInstance attr
    )
{
    public void Execute()
    {
        // enforce file path and namespace matching for classes marked with Lwx attributes
        ProcessorUtils.ValidateFilePathMatchesNamespace(attr.TargetSymbol, ctx);
        var name = ProcessorUtils.SafeIdentifier(attr.TargetSymbol.Name);
        var ns = attr.TargetSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";
        // Extract named arguments (if any) from attribute data
        string? workerName = null;
        string description = string.Empty;
        int threads = 2;
        string stageLiteral = "Lwx.Builders.MicroService.Atributes.LwxStage.None";
        string policyLiteral = "Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy.AlwaysHealthy";

        if (attr.AttributeData != null)
        {
            var named = attr.AttributeData.ToNamedArgumentMap();
            if (named.TryGetValue("Name", out var n) && n.Value is string nsval) workerName = nsval;
            if (named.TryGetValue("Description", out var d) && d.Value is string dval) description = dval;
            if (named.TryGetValue("Threads", out var t) && t.Value is int ival) threads = ival;
            if (named.TryGetValue("Stage", out var s) && s.Value != null)
            {
                var raw = s.Value;
                if (raw is int iv)
                {
                    stageLiteral = iv switch
                    {
                        1 => "Lwx.Builders.MicroService.Atributes.LwxStage.Development",
                        2 => "Lwx.Builders.MicroService.Atributes.LwxStage.Production",
                        _ => "Lwx.Builders.MicroService.Atributes.LwxStage.None"
                    };
                }
                else
                {
                    var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxStage.None";
                    stageLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributes.LwxStage." + tmp);
                }
            }

            if (named.TryGetValue("Policy", out var p) && p.Value != null)
            {
                var raw = p.Value;
                if (raw is int iv)
                {
                    policyLiteral = iv switch
                    {
                        0 => "Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy.UnhealthyIfExit",
                        1 => "Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy.UnhealthyOnException",
                        2 => "Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy.AlwaysHealthy",
                        _ => "Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy.AlwaysHealthy"
                    };
                }
                else
                {
                    var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy.AlwaysHealthy";
                    policyLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributes.LwxWorkerPolicy." + tmp);
                }
            }
        }

        var effectiveName = workerName ?? attr.TargetSymbol.Name;

        // Determine publish short token (for comments)
        var shortStage = stageLiteral != null && stageLiteral.Contains('.')
            ? string.Join('.', stageLiteral.Split('.').Skip(Math.Max(0, stageLiteral.Split('.').Length - 2)))
            : stageLiteral;

        // Inspect constructor parameters for [FromConfig] annotations
        var ctor = (attr.TargetSymbol as INamedTypeSymbol)?.InstanceConstructors
            .Where(c => !c.IsImplicitlyDeclared)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault()
            ?? (attr.TargetSymbol as INamedTypeSymbol)?.InstanceConstructors.FirstOrDefault();

        var configParams = new List<(IParameterSymbol param, string configKey, string propName)>();
        if (ctor != null)
        {
            foreach (var p in ctor.Parameters)
            {
                var fc = p.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "FromConfigAttribute");
                if (fc != null)
                {
                    string nameInAttr = string.Empty;
                    if (fc.ConstructorArguments.Length > 0 && fc.ConstructorArguments[0].Value is string v) nameInAttr = v;
                    if (string.IsNullOrEmpty(nameInAttr)) nameInAttr = p.Name ?? p.ToDisplayString();
                    var propName = ProcessorUtils.PascalSafe(nameInAttr);
                    configParams.Add((p, nameInAttr, propName));
                }
            }
        }

        string configureMethod;
        // Prepare config POCO and factory-based registration snippets if needed
        var configClassSource = string.Empty;
        var factoryRegistration = string.Empty;
        if (configParams.Count > 0)
        {
            var configClassName = ProcessorUtils.SafeIdentifier(attr.TargetSymbol.Name + "Config");
            var sbProps = new System.Text.StringBuilder();
            foreach (var (param, key, propName) in configParams)
            {
                var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sbProps.Append($"public {typeName} {propName} {{ get; set; }} = default!;\n");
            }

            configClassSource = $$"""
                // config POCO used to bind worker configuration
                public class {{configClassName}}
                {
                {{sbProps.FixIndent(2, indentFirstLine: false)}}
                }

                """;

            // Build factory-based registration when config params exist
            var factoryArgs = new System.Text.StringBuilder();
            if (ctor != null)
            {
                var first = true;
                foreach (var p in ctor.Parameters)
                {
                    if (!first) factoryArgs.Append(", ");
                    first = false;
                    var hasCfg = configParams.Any(cp => SymbolEqualityComparer.Default.Equals(cp.param, p));
                    if (hasCfg)
                    {
                        // find matching prop name
                        var match = configParams.First(cp => SymbolEqualityComparer.Default.Equals(cp.param, p));
                        factoryArgs.Append($"cfg.{match.propName}");
                    }
                    else
                    {
                        var pType = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        factoryArgs.Append($"sp.GetRequiredService<{pType}>()");
                    }
                }
            }

            var configClassName2 = ProcessorUtils.SafeIdentifier(attr.TargetSymbol.Name + "Config");

            var bindSnippet = $$"""
                // bind configuration section for this worker
                builder.Services.Configure<{{configClassName2}}>(builder.Configuration.GetSection("{{GeneratorUtils.EscapeForCSharp(effectiveName)}}"));

                """;

            var instancesSnippet = $$"""
                for (int i = 0; i < {{threads}}; i++)
                {
                    builder.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp =>
                    {
                        var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<{{configClassName2}}>>().Value;
                        return new {{attr.TargetSymbol.Name}}({{factoryArgs}});
                    });
                }

                """;

            // The factoryRegistration will be the combined binding + instances snippet
            factoryRegistration = bindSnippet + instancesSnippet;
        }

        if (stageLiteral != null && stageLiteral.EndsWith(".None", System.StringComparison.Ordinal))
        {
            configureMethod = $$"""
                public static void Configure(WebApplicationBuilder builder)
                {
                    // Publish={{shortStage}}
                }
                """;
        }
        else
        {
            var condExpr = stageLiteral != null && stageLiteral.EndsWith(".Development", System.StringComparison.Ordinal)
                ? "builder.Environment.IsDevelopment()"
                : "builder.Environment.IsDevelopment() || builder.Environment.IsProduction()";

            configureMethod = $$"""
                public static void Configure(WebApplicationBuilder builder)
                {
                    // Publish={{shortStage}}
                    if ({{condExpr}})
                    {
                        // Worker name: {{GeneratorUtils.EscapeForCSharp(effectiveName)}}
                        // Description: {{GeneratorUtils.EscapeForCSharp(description)}}
                        // Threads: {{threads}}
                        // Policy: {{policyLiteral}}

                        // Register a descriptor so runtime infrastructure and health systems can discover worker metadata
                        builder.Services.AddSingleton(new LwxWorkerDescriptor { Name = "{{GeneratorUtils.EscapeForCSharp(effectiveName)}}", Description = "{{GeneratorUtils.EscapeForCSharp(description)}}", Threads = {{threads}}, Policy = {{policyLiteral}} });

                        // Register the worker as a hosted service. The worker will be activated according to the application's DI lifecycle.
                        {{(configParams.Count > 0 ? factoryRegistration : $"for (int i = 0; i < {threads}; i++) {{ builder.Services.AddHostedService<{attr.TargetSymbol.Name}>(); }}")}}
                    }
                }
                """;
        }

        var source = $$"""
        // <auto-generated/>
        using System;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.AspNetCore.Builder;
        using Lwx.Builders.MicroService.Atributes;

        namespace {{ns}};

        public partial class {{name}}
        {
            {{configureMethod.FixIndent(1, indentFirstLine: false)}}
        }

        {{configClassSource}}
        """;

        ProcessorUtils.AddGeneratedFile(ctx, $"LwxWorker_{name}.g.cs", source);

        // Register worker type with parent list so ServiceConfig can generate Main wiring
        parent.WorkerNames.Add(ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation));
    }
}
