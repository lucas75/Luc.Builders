using Microsoft.CodeAnalysis;

namespace Lwx.Builders.MicroService.Processors;

internal class LwxSettingPostInitializationProcessor(
    Generator parent,
    IncrementalGeneratorPostInitializationContext ctx
)
{
    public void Execute()
    {
        ProcessorUtils.AddEmbeddedSource(
            ctx,
            "Attributes/LwxSettingAttribute.cs",
            "LwxSettingAttribute.g.cs"
        );
    }
}
