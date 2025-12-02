using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService;

[Generator(LanguageNames.CSharp)]
public class Generator : IIncrementalGenerator
{

    internal readonly List<string> EndpointNames = [];
    internal readonly List<string> WorkerNames = [];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pass 0: Generate static files
        context.RegisterPostInitializationOutput(ctx =>
        {
            new Processors.LwxEndpointPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxWorkerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxServiceBusConsumerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxEventHubConsumerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxTimerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxServiceBusProducerPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxEndpointMetadataPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxEndpointExtensionsPostInitializationProcessor(this, ctx).Execute();
            new Processors.LwxServicePostInitializationProcessor(this, ctx).Execute();
        });

        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => GeneratorUtils.IsPotentialAttribute(node),
                transform: static (ctx, ct) => GeneratorUtils.ResolveAttributeInstance(ctx))
            .Where(x => x is not null)
            .Select(static (x, ct) => x!);

        // First pass: process all attributes except Service

        var lsPass001Attrs = attrProvider.Where(x => x.AttributeName != Processors.LwxConstants.LwxService).Collect();

        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(lsPass001Attrs),
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple;

                foreach (var attr in attrs)
                {
                    if (attr == null) continue;
                    var rp = new Processors.RootProcessor(this, attr, spc, compilation);
                    rp.Execute();
                }
            }
        );

        // Second pass: process Service attributes

        // Validate classes under Endpoints and Workers namespaces are properly annotated.
        context.RegisterSourceOutput(context.CompilationProvider, (spc, compilation) =>
        {
            foreach (var st in compilation.SyntaxTrees)
            {
                var root = st.GetRoot();
                var model = compilation.GetSemanticModel(st);
                var classDecls = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();
                foreach (var cd in classDecls)
                {
                    var sym = model.GetDeclaredSymbol(cd);
                    if (sym == null) continue;

                    // Only inspect source symbols
                    if (!sym.Locations.Any(l => l.IsInSource)) continue;

                    // Skip nested types and private types — rules only apply to top-level public/internal classes
                    if (sym.ContainingType != null) continue;
                    if (sym.DeclaredAccessibility == Accessibility.Private) continue;

                    var ns = sym.ContainingNamespace?.ToDisplayString() ?? string.Empty;

                    if (ns.Contains(".Endpoints", StringComparison.Ordinal))
                    {
                        // Must have LwxEndpoint attribute
                        var has = sym.GetAttributes().Any(a => a.AttributeClass?.Name == "LwxEndpointAttribute");
                        if (!has)
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "LWX018",
                                "Class in Endpoints namespace must be annotated",
                                "Classes declared in namespaces containing '.Endpoints' must be annotated with [LwxEndpoint]. Found: '{0}'",
                                "Usage",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true);

                            var loc = cd.Identifier.GetLocation();
                            spc.ReportDiagnostic(Diagnostic.Create(descriptor, loc, sym.ToDisplayString()));
                        }
                    }

                    if (ns.Contains(".Workers", StringComparison.Ordinal))
                    {
                        var has = sym.GetAttributes().Any(a => a.AttributeClass?.Name == "LwxWorkerAttribute");
                        if (!has)
                        {
                            var descriptor = new DiagnosticDescriptor(
                                "LWX019",
                                "Class in Workers namespace must be annotated",
                                "Classes declared in namespaces containing '.Workers' must be annotated with [LwxWorker]. Found: '{0}'",
                                "Usage",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true);

                            var loc = cd.Identifier.GetLocation();
                            spc.ReportDiagnostic(Diagnostic.Create(descriptor, loc, sym.ToDisplayString()));
                        }
                    }
                }
            }
        });

        var lsPass002Attrs = attrProvider.Where(x => x.AttributeName == Processors.LwxConstants.LwxService).Collect();

        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(lsPass002Attrs),
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple;

                var scList = attrs.ToArray();
                if (scList.Length == 0)
                {
                    Processors.LwxServiceTypeProcessor.ReportMissingService(spc);
                }
                else if (scList.Length > 1)
                {
                    foreach (var sc in scList)
                    {
                        Processors.LwxServiceTypeProcessor.ReportMultipleService(spc, sc!.Location);
                    }
                }
                else
                {
                    var rp = new Processors.RootProcessor(this, scList[0]!, spc, compilation);
                    rp.Execute();
                }
            }
        );
    }
}

