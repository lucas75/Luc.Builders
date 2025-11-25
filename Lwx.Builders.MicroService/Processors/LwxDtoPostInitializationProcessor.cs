using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService.Processors;
using System.Text;
using Lwx.Builders.MicroService;

namespace Lwx.Builders.MicroService.Processors;

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
