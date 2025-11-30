using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxEndpointMetadataPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxEndpointMetadata.cs",
          "LwxEndpointMetadata.g.cs"
        );
    }
}