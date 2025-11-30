using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Lwx.Builders.MicroService.Processors;

internal sealed class RootProcessor
(
  Generator parent, 
  FoundAttribute foundAttribute, 
  SourceProductionContext ctx, 
  Compilation compilation
)
{
  public void Execute()
  {    
    if (foundAttribute.AttributeName == LwxConstants.LwxEndpoint)
    {
      new LwxEndpointTypeProcessor(foundAttribute, ctx, compilation).Execute();
      var fullName = foundAttribute.TargetSymbol.ToDisplayString();
      var asmName = compilation.AssemblyName ?? string.Empty;
      if (!string.IsNullOrEmpty(asmName) && fullName.StartsWith(asmName + ".", System.StringComparison.Ordinal))
      {
        fullName = fullName.Substring(asmName.Length + 1);
      }
      parent.EndpointNames.Add(fullName);
      return;
    }

    if (foundAttribute.AttributeName == LwxConstants.LwxWorker)
    {
      new LwxWorkerTypeProcessor(foundAttribute, ctx, compilation).Execute();
      var fullName = foundAttribute.TargetSymbol.ToDisplayString();
      var asmName = compilation.AssemblyName ?? string.Empty;
      if (!string.IsNullOrEmpty(asmName) && fullName.StartsWith(asmName + ".", System.StringComparison.Ordinal))
      {
        fullName = fullName.Substring(asmName.Length + 1);
      }
      parent.WorkerNames.Add(fullName);
      return;
    }

    if (foundAttribute.AttributeName == LwxConstants.LwxServiceBusConsumer)
    {
      new LwxServiceBusConsumerTypeProcessor(foundAttribute, ctx, compilation).Execute();
      return;
    }

    if (foundAttribute.AttributeName == LwxConstants.LwxEventHubConsumer)
    {
      new LwxEventHubConsumerTypeProcessor(foundAttribute, ctx, compilation).Execute();
      return;
    }

    if (foundAttribute.AttributeName == LwxConstants.LwxTimer)
    {
      new LwxTimerTypeProcessor(foundAttribute, ctx, compilation).Execute();
      return;
    }

    if (foundAttribute.AttributeName == LwxConstants.LwxServiceBusProducer)
    {
      new LwxServiceBusProducerTypeProcessor(foundAttribute, ctx, compilation).Execute();
      return;
    }

    if (foundAttribute.AttributeName == LwxConstants.LwxServiceConfig)
    {
      // If we've already processed a ServiceConfig attribute for this compilation, report an error and skip
      if (parent.ServiceConfigLocation != Location.None)
      {
        var existingFile = parent.ServiceConfigLocation.SourceTree?.FilePath ?? "(unknown)";
        var descriptor = new DiagnosticDescriptor(
          "LWX017",
          "Multiple ServiceConfig declarations",
          "Only one [LwxServiceConfig] declaration is allowed; another was already found at '{0}'",
          "Configuration",
          DiagnosticSeverity.Error,
          isEnabledByDefault: true);

        ctx.ReportDiagnostic(Diagnostic.Create(descriptor, foundAttribute.Location, existingFile));
        return;
      }
      // Pass already-collected endpoint and worker names so the ServiceConfig processor
      // can generate the LwxEndpointExtensions and optionally the Program Main file.
      new LwxServiceConfigTypeProcessor(foundAttribute, ctx, compilation).Execute(parent);

      parent.ServiceConfigLocation = foundAttribute.Location;
      parent.ServiceConfigSymbol = foundAttribute.TargetSymbol as INamedTypeSymbol;

      var attrData = foundAttribute.AttributeData;
      parent.LwxServiceConfigAttributeData = attrData;
      if (attrData != null)
      {
        var named = attrData.ToNamedArgumentMap();
        if (named.TryGetValue("GenerateMain", out var gm) && gm.Value is bool b)
        {
          parent.GenerateMainFlag = b;
        }
      }

      return;
    }
  }
}
