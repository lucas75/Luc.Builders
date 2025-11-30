using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Lwx.Builders.Dto.Processors;

namespace Lwx.Builders.Dto.Processors;

internal class LwxDtoPostInitializationProcessor(
    DtoGenerator parent,
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
