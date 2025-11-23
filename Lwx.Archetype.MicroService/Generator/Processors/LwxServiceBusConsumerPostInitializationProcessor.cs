using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator;
using Lwx.Archetype.MicroService.Generator.Processors;
using System.Text;
using System.Linq;
using System;

namespace Lwx.Archetype.MicroService.Generator.Processors;

internal class LwxServiceBusConsumerPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxServiceBusConsumerAttribute.cs",
          "LwxServiceBusConsumerAttribute.g.cs"
        );
    }
}
