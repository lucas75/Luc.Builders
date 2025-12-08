using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService.Processors;

/// <summary>
/// Processor that emits the LwxMessageSource attribute and related interfaces via post-initialization.
/// </summary>
internal class LwxMessageSourcePostInitializationProcessor(Generator parent, IncrementalGeneratorPostInitializationContext ctx)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxMessageSourceAttribute.cs",
            "LwxMessageSourceAttribute.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxMessageEndpointDescriptor.cs",
            "LwxMessageEndpointDescriptor.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/ILwxQueueMessage.cs",
            "ILwxQueueMessage.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/ILwxQueueProvider.cs",
            "ILwxQueueProvider.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/ILwxErrorPolicy.cs",
            "ILwxErrorPolicy.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/ILwxProviderErrorPolicy.cs",
            "ILwxProviderErrorPolicy.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDefaultErrorPolicy.cs",
            "LwxDefaultErrorPolicy.g.cs"
        );
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxDefaultProviderErrorPolicy.cs",
            "LwxDefaultProviderErrorPolicy.g.cs"
        );
    }
}
