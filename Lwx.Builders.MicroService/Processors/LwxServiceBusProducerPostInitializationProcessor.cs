using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService;
using Lwx.Builders.MicroService.Processors;
using System.Text;
using System;
using System.Linq;

namespace Lwx.Builders.MicroService.Processors;

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
