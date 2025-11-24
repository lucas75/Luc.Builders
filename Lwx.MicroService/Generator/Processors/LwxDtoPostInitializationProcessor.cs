using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.MicroService.Generator.Processors;
using System.Text;

namespace Lwx.MicroService.Generator.Processors;

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
        GeneratorHelpers.AddEmbeddedSource(
            ctx,
            "Attributes/DtoType.cs",
            "DtoType.g.cs"
        );
        GeneratorHelpers.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDtoPropertyAttribute.cs",
            "LwxDtoPropertyAttribute.g.cs"
        );
        GeneratorHelpers.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDtoIgnoreAttribute.cs",
            "LwxDtoIgnoreAttribute.g.cs"
        );
    }
}
