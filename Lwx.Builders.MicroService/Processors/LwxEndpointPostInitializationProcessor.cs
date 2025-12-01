using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService;
using Lwx.Builders.MicroService.Processors;
using System.Text;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxEndpointPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxEndpointAttribute.cs",
          "LwxEndpointAttribute.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxStage.cs",
          "LwxStage.g.cs"
        );
    }
}
