using Microsoft.CodeAnalysis;
using System;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService;
using Lwx.Builders.MicroService.Processors;
using System.Linq;
using System.Text;

namespace Lwx.Builders.MicroService.Processors;

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
