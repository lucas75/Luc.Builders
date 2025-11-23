using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator;
using Lwx.Archetype.MicroService.Generator.Processors;
using System.Text;

namespace Lwx.Archetype.MicroService.Generator.Processors;

internal class LwxEndpointPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxEndpointAttribute.cs",
          "LwxEndpointAttribute.g.cs"
        );
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxStage.cs",
          "LwxStage.g.cs"
        );
    }
}
