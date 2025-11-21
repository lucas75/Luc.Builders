using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator.Processors;
using System.Text;

namespace Lwx.Archetype.MicroService.Generator.Processors;

internal class LwxDtoPostInitializationProcessor(
    IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
            ctx, 
            "Attributes/LwxDtoAttribute.cs", 
            "LwxDtoAttribute.g.cs"
        );
    }
}
