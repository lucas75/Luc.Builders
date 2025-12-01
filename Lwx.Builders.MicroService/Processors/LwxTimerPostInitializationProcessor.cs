using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Lwx.Builders.MicroService;
using Lwx.Builders.MicroService.Processors;
using System.Text;
using System;
using System.Linq;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxTimerPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxTimerAttribute.cs",
          "LwxTimerAttribute.g.cs"
        );
    }
}
