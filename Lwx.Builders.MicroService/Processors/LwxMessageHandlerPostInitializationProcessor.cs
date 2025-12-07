using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxMessageHandlerPostInitializationProcessor(
  Generator parent,
  IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        // Emit the LwxMessageHandler attribute
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxMessageHandlerAttribute.cs",
          "LwxMessageHandlerAttribute.g.cs"
        );

        // Emit the queue message interface
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/ILwxQueueMessage.cs",
          "ILwxQueueMessage.g.cs"
        );

        // Emit the queue provider interface
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/ILwxQueueProvider.cs",
          "ILwxQueueProvider.g.cs"
        );

        // Emit the error policy interface
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/ILwxErrorPolicy.cs",
          "ILwxErrorPolicy.g.cs"
        );

        // Emit the provider error policy interface
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/ILwxProviderErrorPolicy.cs",
          "ILwxProviderErrorPolicy.g.cs"
        );

        // Emit the default error policy
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxDefaultErrorPolicy.cs",
          "LwxDefaultErrorPolicy.g.cs"
        );

        // Emit the default provider error policy
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxDefaultProviderErrorPolicy.cs",
          "LwxDefaultProviderErrorPolicy.g.cs"
        );

        // Emit the message handler descriptor
        ProcessorUtils.AddEmbeddedSource(
          ctx,
          "Attributes/LwxMessageHandlerDescriptor.cs",
          "LwxMessageHandlerDescriptor.g.cs"
        );
    }
}
