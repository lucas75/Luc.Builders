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
using System.Security.Cryptography;

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
            new Processors.LwxServiceConfigPostInitializationProcessor(this, ctx).Execute();
        });
        
        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPotentialAttribute(node),
                transform: static (ctx, ct) => Transform(ctx))
            .Where(x => x is not null);
               
        // First pass: process all attributes except ServiceConfig
        
        var lsPass001Attrs = attrProvider.Where(x => x != null && x.AttributeName != LwxConstants.LwxServiceConfig).Collect();
                
        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(lsPass001Attrs), 
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple;            

                foreach (var attr in attrs)
                {
                    if (attr == null) continue;
                    var rp = new RootProcessor(this, attr, spc, compilation);
                    rp.Execute();
                }
            }
        );

        // Second pass: process ServiceConfig attributes

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
        
        var lsPass002Attrs = attrProvider.Where(x => x != null && x.AttributeName == LwxConstants.LwxServiceConfig).Collect();

        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(lsPass002Attrs), 
            (spc, tuple) =>
            {
                var (compilation, attrs) = tuple; 

                var scList = attrs.ToArray();
                if (scList.Length == 0)
                {
                    Processors.LwxServiceConfigTypeProcessor.ReportMissingServiceConfig(spc);
                }
                else if (scList.Length > 1)
                {
                    foreach (var sc in scList)
                    {
                        Processors.LwxServiceConfigTypeProcessor.ReportMultipleServiceConfig(spc, sc!.Location);
                    }
                }
                else
                {
                    var rp = new RootProcessor(this, scList[0]!, spc, compilation);
                    rp.Execute();
                }
            }
        );
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
        var name = attribute.Name.ToString();
        var simple = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
        if (simple.EndsWith("Attribute")) simple = simple[..^"Attribute".Length];
        return LwxConstants.AttributeNames.Contains(simple, StringComparer.Ordinal);
    }
}

