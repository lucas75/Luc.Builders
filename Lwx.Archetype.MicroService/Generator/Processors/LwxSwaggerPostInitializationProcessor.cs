using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Archetype.MicroService.Generator;
using Lwx.Archetype.MicroService.Generator.Processors;
using System.Text;

namespace Lwx.Archetype.MicroService.Generator.Processors;

internal class LwxSwaggerPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
  public void Execute()
  {
    GeneratorHelpers.AddEmbeddedSource(
      ctx, 
      "Attributes/LwxSwaggerAttribute.cs", 
      "LwxSwaggerAttribute.g.cs"
    );
  }
}