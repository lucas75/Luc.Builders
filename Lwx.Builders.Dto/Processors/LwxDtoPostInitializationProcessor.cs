using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Lwx.Builders.Dto.Processors;
using Lwx.Builders.Dto;

namespace Lwx.Builders.Dto.Processors;

internal class LwxDtoPostInitializationProcessor(
    DtoGenerator parent,
    IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDtoAttribute.cs",
            "LwxDtoAttribute.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/DtoType.cs",
            "DtoType.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDtoPropertyAttribute.cs",
            "LwxDtoPropertyAttribute.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDtoIgnoreAttribute.cs",
            "LwxDtoIgnoreAttribute.g.cs"
        );
    }
}
