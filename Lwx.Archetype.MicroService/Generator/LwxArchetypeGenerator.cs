using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator.Processors;

namespace Lwx.Archetype.MicroService.Generator;

internal sealed class FoundAttribute(string attributeName, ISymbol targetSymbol, Location location, AttributeData attributeData)
{
  public string AttributeName { get; } = attributeName;
  public ISymbol TargetSymbol { get; } = targetSymbol;
  public Location Location { get; } = location;
  public AttributeData AttributeData { get; } = attributeData;
}

[Generator(LanguageNames.CSharp)]
public class LwxArchetypeGenerator : IIncrementalGenerator
{
    private static readonly string[] AttributeNames = [
        "LwxEndpoint",
        "LwxDto",
        "LwxWorker",
        "LwxServiceBusConsumer",
        "LwxEventHubConsumer",
        "LwxTimer",
        "LwxServiceBusProducer",
        "LwxSwagger"
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generate attribute definitions so consumer projects don't need to declare them.
        context.RegisterPostInitializationOutput(ctx =>
        {
            // Use each processor's GenerateAttribute method
            new LwxEndpointPostInitializationProcessor(ctx).Execute();
            new LwxDtoPostInitializationProcessor(ctx).Execute();
            new LwxWorkerPostInitializationProcessor(ctx).Execute();
            new LwxServiceBusConsumerPostInitializationProcessor(ctx).Execute();
            new LwxEventHubConsumerPostInitializationProcessor(ctx).Execute();
            new LwxTimerPostInitializationProcessor(ctx).Execute();
            new LwxServiceBusProducerPostInitializationProcessor(ctx).Execute();
            new LwxEndpointMetadataPostInitializationProcessor(ctx).Execute();
            new LwxEndpointExtensionsPostInitializationProcessor(ctx).Execute();
            new LwxSwaggerPostInitializationProcessor(ctx).Execute();
        });

        // Find attributes whose simple name matches one of the attribute names in our list
        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPotentialAttribute(node),
                transform: static (ctx, ct) => Transform(ctx))
            .Where(x => x is not null);

        // Collect all found attributes and process them together
        context.RegisterSourceOutput(context.CompilationProvider.Combine(attrProvider.Collect()), (spc, tuple) =>
        {
            var compilation = tuple.Left;
            var found = tuple.Right;
            foreach (var f in found)
            {
                switch (f.AttributeName)
                {
                    case "LwxEndpoint":
                        new LwxEndpointTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxDto":
                        new LwxDtoTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxWorker":
                        new LwxWorkerTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxServiceBusConsumer":
                        new LwxServiceBusConsumerTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxEventHubConsumer":
                        new LwxEventHubConsumerTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxTimer":
                        new LwxTimerTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxServiceBusProducer":
                        new LwxServiceBusProducerTypeProcessor(f, spc, compilation).Execute();
                        break;
                    case "LwxSwagger":
                        new LwxSwaggerTypeProcessor(f, spc, compilation).Execute();
                        break;
                }
            }
        });
    }

    private static FoundAttribute Transform(GeneratorSyntaxContext ctx)
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
        if (!AttributeNames.Contains(attrName, StringComparer.Ordinal)) return default(FoundAttribute);

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
        return AttributeNames.Contains(simple, StringComparer.Ordinal);
    }    
}
