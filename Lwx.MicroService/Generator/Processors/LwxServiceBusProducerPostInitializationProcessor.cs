using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.MicroService.Generator;
using Lwx.MicroService.Generator.Processors;
using System.Text;
using System;
using System.Linq;

namespace Lwx.MicroService.Generator.Processors;

internal class LwxServiceBusProducerPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxServiceBusProducerAttribute.cs",
          "LwxServiceBusProducerAttribute.g.cs"
        );
    }
}
