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
        string stageLiteral = "Lwx.Builders.MicroService.Atributtes.LwxStage.None";
        string policyLiteral = "Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy.AlwaysHealthy";

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

            if (named.TryGetValue("Policy", out var p) && p.Value != null)
            {
                var raw = p.Value;
                if (raw is int iv)
                {
                    policyLiteral = iv switch
                    {
                        0 => "Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy.UnhealthyIfExit",
                        1 => "Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy.UnhealthyOnException",
                        2 => "Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy.AlwaysHealthy",
                        _ => "Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy.AlwaysHealthy"
                    };
                }
                else
                {
                    var tmp = raw.ToString() ?? "Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy.AlwaysHealthy";
                    policyLiteral = tmp.Contains('.') ? tmp : ("Lwx.Builders.MicroService.Atributtes.LwxWorkerPolicy." + tmp);
                }
            }
        }

        var effectiveName = workerName ?? attr.TargetSymbol.Name;

        // Determine publish short token (for comments)
        var shortStage = stageLiteral != null && stageLiteral.Contains('.')
            ? string.Join('.', stageLiteral.Split('.').Skip(Math.Max(0, stageLiteral.Split('.').Length - 2)))
            : stageLiteral;

        string configureMethod;

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
            var condExpr = stageLiteral != null && stageLiteral.EndsWith(".DevelopmentOnly", System.StringComparison.Ordinal)
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
                        for (int i = 0; i < {{threads}}; i++) { builder.Services.AddHostedService<{{attr.TargetSymbol.Name}}>(); }
                    }
                }
                """;
        }

        var source = $$"""
        // <auto-generated/>
        using System;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.Extensions.Hosting;
        using Lwx.Builders.MicroService.Atributtes;

        namespace {{ns}};

        public partial class {{name}}
        {
            {{configureMethod.FixIndent(1, indentFirstLine: false)}}
        }

        """;

        ProcessorUtils.AddGeneratedFile(ctx, $"LwxWorker_{name}.g.cs", source);

        // Register worker type with the service registration based on namespace
        var servicePrefix = Generator.ComputeServicePrefix(ns);
        var reg = parent.GetOrCreateRegistration(servicePrefix);
        reg.WorkerNames.Add(ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation));
        // Also register worker metadata like thread count so service helper can print it
        reg.WorkerInfos.Add((ProcessorUtils.ExtractRelativeTypeName(attr.TargetSymbol, compilation), threads));
    }
}
