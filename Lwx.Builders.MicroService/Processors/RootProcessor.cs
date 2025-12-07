using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Lwx.Builders.MicroService.Processors;

internal sealed class RootProcessor
(
  Generator parent,
  AttributeInstance attr,
  SourceProductionContext ctx,
  Compilation compilation
)
{
    public void Execute()
    {
        switch (attr.AttributeName)
        {
            case LwxConstants.LwxEndpoint:
                new LwxEndpointTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxWorker:
                new LwxWorkerTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxServiceBusConsumer:
                new LwxServiceBusConsumerTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxEventHubConsumer:
                new LwxEventHubConsumerTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxTimer:
                new LwxTimerTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxServiceBusProducer:
                new LwxServiceBusProducerTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxService:
                new LwxServiceTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxSetting:
                new LwxSettingTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            case LwxConstants.LwxMessageHandler:
                new LwxMessageHandlerTypeProcessor(parent, compilation, ctx, attr).Execute();
                break;

            default:
                break;
        }
    }
}
