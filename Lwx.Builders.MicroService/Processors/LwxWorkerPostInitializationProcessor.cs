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

        // Also embed the worker policy enum so consuming projects receive the full contract
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxWorkerPolicy.cs",
          "LwxWorkerPolicy.g.cs"
        );

        // Embed FromConfig attribute so consumers can annotate parameters
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/FromConfigAttribute.cs",
          "FromConfigAttribute.g.cs"
        );

        // Expose a worker descriptor type to consumer projects used for health/metadata
        GeneratorHelpers.AddEmbeddedSource(
          ctx,
          "Attributes/LwxWorkerDescriptor.cs",
          "LwxWorkerDescriptor.g.cs"
        );
    }
}
