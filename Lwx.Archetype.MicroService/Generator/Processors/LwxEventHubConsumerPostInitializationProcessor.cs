using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator;
using Lwx.Archetype.MicroService.Generator.Processors;
using System.Text;

namespace Lwx.Archetype.MicroService.Generator.Processors;

internal class LwxEventHubConsumerPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxEventHubConsumerAttribute.cs",
          "LwxEventHubConsumerAttribute.g.cs"
        );
    }
}
