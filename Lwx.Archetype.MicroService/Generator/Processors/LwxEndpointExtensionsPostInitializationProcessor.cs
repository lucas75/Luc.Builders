using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;

namespace Lwx.Archetype.MicroService.Generator.Processors;

internal class LwxEndpointExtensionsPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
  public void Execute()
  {
    GeneratorHelpers.AddEmbeddedSource(
      ctx, 
      "Generator/Templates/LwxEndpointExtensions.cs", 
      "LwxEndpointExtensions.g.cs"
    );
  }
}