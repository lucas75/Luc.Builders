using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxEndpointExtensionsPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        // LwxConfigure is now generated dynamically in the main generator
    }
}