using System;
#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

namespace Lwx.Builders.Dto;

[Generator(LanguageNames.CSharp)]
public class DtoGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Emit attribute sources into consumer projects
        context.RegisterPostInitializationOutput(ctx =>
        {
            new Processors.LwxDtoPostInitializationProcessor(this, ctx).Execute();
        });

        var attrProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, ct) => IsPotentialAttribute(node),
                transform: static (ctx, ct) => Transform(ctx))
            .Where(x => x is not null);

        context.RegisterSourceOutput(context.CompilationProvider.Combine(attrProvider.Collect()), (spc, tuple) =>
        {
            var compilation = tuple.Left;
            var found = tuple.Right;
            foreach (var f in found)
            {
                if (f == null) continue;
                if (f.AttributeName == Processors.LwxConstants.LwxDto)
                {
                    new Processors.LwxDtoTypeProcessor(this, compilation, spc, f).Execute();
                }
            }
        });
    }

    private static Processors.FoundAttribute? Transform(GeneratorSyntaxContext ctx)
    {
        var attributeSyntax = (AttributeSyntax)ctx.Node;

        var info = ctx.SemanticModel.GetSymbolInfo(attributeSyntax, CancellationToken.None);
        var attrType = info.Symbol switch
        {
            IMethodSymbol ms => ms.ContainingType,
            INamedTypeSymbol nts => nts,
            _ => null
        };

        if (attrType == null) return default(Processors.FoundAttribute);

        var attrName = attrType.Name;
        if (attrName.EndsWith("Attribute")) attrName = attrName.Substring(0, attrName.Length - "Attribute".Length);
        if (!Processors.LwxConstants.AttributeNames.Contains(attrName, StringComparer.Ordinal)) return default(Processors.FoundAttribute);

        var parent = attributeSyntax.Parent?.Parent;
        if (parent == null) return default(Processors.FoundAttribute);

        var declaredSymbol = ctx.SemanticModel.GetDeclaredSymbol(parent, CancellationToken.None);
        if (declaredSymbol == null) return default(Processors.FoundAttribute);

        var attrData = declaredSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass != null && ad.AttributeClass.ToDisplayString() == attrType.ToDisplayString());

        return new Processors.FoundAttribute(attrName, declaredSymbol, parent.GetLocation(), attrData);
    }

    private static bool IsPotentialAttribute(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute) return false;
        var name = attribute.Name.ToString();
        var simple = name.Contains('.') ? name.Substring(name.LastIndexOf('.') + 1) : name;
        if (simple.EndsWith("Attribute")) simple = simple.Substring(0, simple.Length - "Attribute".Length);
        return Processors.LwxConstants.AttributeNames.Contains(simple, StringComparer.Ordinal);
    }
}
