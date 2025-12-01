using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService;
using Lwx.Builders.MicroService.Processors;
using System.Text;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxServiceConfigPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxServiceConfigAttribute.cs",
          "LwxServiceConfigAttribute.g.cs"
        );
    }
}
