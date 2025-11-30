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

