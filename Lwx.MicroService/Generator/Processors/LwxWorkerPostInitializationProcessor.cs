using Microsoft.CodeAnalysis;
using System;
using Microsoft.CodeAnalysis.Text;
using Lwx.MicroService.Generator;
using Lwx.MicroService.Generator.Processors;
using System.Linq;
using System.Text;

namespace Lwx.MicroService.Generator.Processors;

internal class LwxWorkerPostInitializationProcessor(
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxWorkerAttribute.cs",
          "LwxWorkerAttribute.g.cs"
        );
    }
}
