using Lwx.MicroService.Atributes;

namespace Lwx.MicroService.Generator;

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
    public const string LwxServiceConfigAttribute = nameof(LwxServiceConfigAttribute);

    public static readonly string LwxEndpoint = LwxEndpointAttribute.Replace("Attribute", "");
    public static readonly string LwxDto = LwxDtoAttribute.Replace("Attribute", "");
    public static readonly string LwxDtoProperty = LwxDtoPropertyAttribute.Replace("Attribute", "");
    public static readonly string LwxDtoIgnore = LwxDtoIgnoreAttribute.Replace("Attribute", "");
    public static readonly string LwxWorker = LwxWorkerAttribute.Replace("Attribute", "");
    public static readonly string LwxServiceBusConsumer = LwxServiceBusConsumerAttribute.Replace("Attribute", "");
    public static readonly string LwxEventHubConsumer = LwxEventHubConsumerAttribute.Replace("Attribute", "");
    public static readonly string LwxTimer = LwxTimerAttribute.Replace("Attribute", "");
    public static readonly string LwxServiceBusProducer = LwxServiceBusProducerAttribute.Replace("Attribute", "");
    // LwxSwagger was removed: use LwxServiceConfig instead
    public static readonly string LwxServiceConfig = LwxServiceConfigAttribute.Replace("Attribute", "");

    public static readonly string[] AttributeNames = [
        LwxEndpoint,
        LwxDto,
        LwxDtoProperty,
        LwxWorker,
        LwxServiceBusConsumer,
        LwxEventHubConsumer,
        LwxTimer,
        LwxServiceBusProducer,
        LwxServiceConfig
    ];
}