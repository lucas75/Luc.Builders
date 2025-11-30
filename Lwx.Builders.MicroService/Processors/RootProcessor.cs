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
    switch (foundAttribute.AttributeName)
    {
      case LwxConstants.LwxEndpoint:
        new LwxEndpointTypeProcessor(foundAttribute, ctx, compilation).Execute();
        parent.EndpointNames.Add(GeneratorHelpers.ExtractRelativeTypeName(foundAttribute.TargetSymbol, compilation));
        break;

      case LwxConstants.LwxWorker:
        new LwxWorkerTypeProcessor(foundAttribute, ctx, compilation).Execute();
        parent.WorkerNames.Add(GeneratorHelpers.ExtractRelativeTypeName(foundAttribute.TargetSymbol, compilation));
        break;

      case LwxConstants.LwxServiceBusConsumer:
        new LwxServiceBusConsumerTypeProcessor(foundAttribute, ctx, compilation).Execute();
        break;

      case LwxConstants.LwxEventHubConsumer:
        new LwxEventHubConsumerTypeProcessor(foundAttribute, ctx, compilation).Execute();
        break;

      case LwxConstants.LwxTimer:
        new LwxTimerTypeProcessor(foundAttribute, ctx, compilation).Execute();
        break;

      case LwxConstants.LwxServiceBusProducer:
        new LwxServiceBusProducerTypeProcessor(foundAttribute, ctx, compilation).Execute();
        break;

      case LwxConstants.LwxServiceConfig:
        new LwxServiceConfigTypeProcessor(foundAttribute, ctx, compilation, parent).Execute();
        break;

      default:
        break;
    }
  }
}
