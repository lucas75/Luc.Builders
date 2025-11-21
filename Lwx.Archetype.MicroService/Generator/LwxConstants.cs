using Lwx.Archetype.MicroService.Atributes;

namespace Lwx.Archetype.MicroService.Generator;

public static class LwxConstants
{
    public const string LwxEndpointAttribute = nameof(LwxEndpointAttribute);
    public const string LwxDtoAttribute = nameof(LwxDtoAttribute);
    public const string LwxDtoPropertyAttribute = nameof(LwxDtoPropertyAttribute);
    public const string LwxDtoIgnoreAttribute = nameof(LwxDtoIgnoreAttribute);
    public const string LwxWorkerAttribute = nameof(LwxWorkerAttribute);
    public const string LwxServiceBusConsumerAttribute = nameof(LwxServiceBusConsumerAttribute);
    public const string LwxEventHubConsumerAttribute = nameof(LwxEventHubConsumerAttribute);
    public const string LwxTimerAttribute = nameof(LwxTimerAttribute);
    public const string LwxServiceBusProducerAttribute = nameof(LwxServiceBusProducerAttribute);
    public const string LwxSwaggerAttribute = nameof(LwxSwaggerAttribute);

    public static readonly string LwxEndpoint = LwxEndpointAttribute.Replace("Attribute", "");
    public static readonly string LwxDto = LwxDtoAttribute.Replace("Attribute", "");
    public static readonly string LwxDtoProperty = LwxDtoPropertyAttribute.Replace("Attribute", "");
    public static readonly string LwxDtoIgnore = LwxDtoIgnoreAttribute.Replace("Attribute", "");
    public static readonly string LwxWorker = LwxWorkerAttribute.Replace("Attribute", "");
    public static readonly string LwxServiceBusConsumer = LwxServiceBusConsumerAttribute.Replace("Attribute", "");
    public static readonly string LwxEventHubConsumer = LwxEventHubConsumerAttribute.Replace("Attribute", "");
    public static readonly string LwxTimer = LwxTimerAttribute.Replace("Attribute", "");
    public static readonly string LwxServiceBusProducer = LwxServiceBusProducerAttribute.Replace("Attribute", "");
    public static readonly string LwxSwagger = LwxSwaggerAttribute.Replace("Attribute", "");

    public static readonly string[] AttributeNames = [
        LwxEndpoint,
        LwxDto,
        LwxDtoProperty,
        LwxWorker,
        LwxServiceBusConsumer,
        LwxEventHubConsumer,
        LwxTimer,
        LwxServiceBusProducer,
        LwxSwagger
    ];
}