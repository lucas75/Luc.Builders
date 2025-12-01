using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService;
using Lwx.Builders.MicroService.Processors;
using System.Text;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxEventHubConsumerPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxEventHubConsumerAttribute.cs",
          "LwxEventHubConsumerAttribute.g.cs"
        );
    }
}
