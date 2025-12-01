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
                predicate: static (node, ct) => GeneratorUtils.IsPotentialAttribute(node),
                transform: static (ctx, ct) => GeneratorUtils.ResolveFoundAttribute(ctx))
            .Where(x => x is not null)
            .Select(static (x, ct) => x!);

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

    // Transform logic moved to GeneratorUtils.ResolveFoundAttribute

    // IsPotentialAttribute moved to GeneratorUtils.IsPotentialAttribute
}
