using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator;
using Lwx.Archetype.MicroService.Generator.Processors;
using System.Text;
using System;
using System.Linq;

namespace Lwx.Archetype.MicroService.Generator.Processors;

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
