# TODO

## Pending Features

### LwxTimer Mechanism

The builder should support cron-based scheduled execution via `[LwxTimer]` attribute:

```csharp
[LwxTimer(
   CronExpression = "0 */5 * * * *",  // Every 5 minutes
   Stage = LwxStage.All
)]
public partial class EndpointProc001Timer
{
    public static async Task Execute(ILogger<EndpointProc001Timer> logger)
    {
        logger.LogInformation("Timer executed at {Time}", DateTimeOffset.Now);
    }
}
```

**Implementation Notes:**
- Timer runs as a singleton hosted service
- Uses NCrontab or similar for cron expression parsing
- Stage controls which environments the timer runs in
- DI parameters are resolved at execution time
- Consider adding: `RunOnStartup`, `MaxConcurrentExecutions`, `MissedExecutionPolicy`

### Method-Level Attributes

Consider moving attributes from class level to method level for more flexibility:

```csharp
public partial class OrderEndpoints
{
    [LwxEndpoint(Uri = "GET /orders/{id}")]
    public static Task<OrderDto> GetOrder([FromRoute] int id) { }

    [LwxEndpoint(Uri = "POST /orders")]
    public static Task<OrderDto> CreateOrder([FromBody] CreateOrderDto dto) { }
}
```

---

# CHANGELOG

## ✅ Completed: LwxMessageEndpoint Mechanism (December 2025)

Implemented message queue processing with configurable providers and error policies.
Refactored from LwxMessageHandler to LwxMessageEndpoint with split stage control.

### Core Interfaces
- `ILwxQueueProvider` - Queue provider abstraction with Configure/Start/Stop lifecycle
- `ILwxQueueMessage` - Message abstraction with Complete/Abandon/DeadLetter lifecycle
- `ILwxErrorPolicy` - Handler-level error policy for message processing errors
- `ILwxProviderErrorPolicy` - Provider-level error policy for connection/worker errors

### Default Policies
- `LwxDefaultErrorPolicy` - Dead-letters messages on handler errors
- `LwxDefaultProviderErrorPolicy` - Logs provider errors

### `[LwxMessageEndpoint]` Attribute Properties
- `Uri` - HTTP endpoint path for receiving messages (e.g., "POST /receive-order")
- `QueueStage` - Stage for queue consumer (None, DevelopmentOnly, All)
- `UriStage` - Stage for HTTP endpoint (None, DevelopmentOnly, All)
- `QueueProvider` - Type implementing `ILwxQueueProvider`
- `QueueConfigSection` - Configuration section name (reads from `Queues:{section}`)
- `QueueReaders` - Concurrency level (default: 2)
- `HandlerErrorPolicy` - Type implementing `ILwxErrorPolicy` (optional)
- `ProviderErrorPolicy` - Type implementing `ILwxProviderErrorPolicy` (optional)

### Naming Convention
- Classes: `EndpointMsg{PathSegments}` (e.g., `EndpointMsgReceiveOrder`)
- Namespace: Must be under `.Endpoints` (same as regular endpoints)
- Queue providers: Should be in `.Services` namespace

### Generated Code
- Hosted background service for queue consumption with DI support
- HTTP endpoint for manual message submission (development testing)
- Service wiring via `ConfigureMessageEndpoints(builder)` and `ConfigureMessageEndpoints(app)`

### New Diagnostics
- `LWX040` - Invalid message endpoint class name
- `LWX041` - Message endpoint name does not match URI
- `LWX042` - Invalid message endpoint namespace
- `LWX043` - Missing QueueProvider
- `LWX044` - QueueProvider type not found
- `LWX045` - QueueProvider must implement ILwxQueueProvider
- `LWX046` - HandlerErrorPolicy type not found
- `LWX047` - HandlerErrorPolicy must implement ILwxErrorPolicy
- `LWX048` - ProviderErrorPolicy type not found
- `LWX049` - ProviderErrorPolicy must implement ILwxProviderErrorPolicy

### Example Usage

```csharp
// In Services/ folder - queue provider
public class MyQueueProvider : ILwxQueueProvider { /* ... */ }

// In Endpoints/ folder - message endpoint
[LwxMessageEndpoint(
    Uri = "POST /receive-order",
    QueueStage = LwxStage.All,           // Queue runs everywhere
    UriStage = LwxStage.DevelopmentOnly, // HTTP only in dev
    QueueProvider = typeof(MyQueueProvider),
    QueueConfigSection = "OrderQueue",
    QueueReaders = 2
)]
public partial class EndpointMsgReceiveOrder
{
    // DI parameters are resolved at execution time
    public static Task Execute(
        ILwxQueueMessage msg,
        ILogger<EndpointMsgReceiveOrder> logger,
        IConfiguration config)
    {
        // Process the message
        return msg.CompleteAsync().AsTask();
    }
}
```

Configuration in appsettings.json:
```json
{
  "Queues": {
    "OrderQueue": {
      "Readers": 2,
      "ProviderSpecificSetting": "value"
    }
  }
}
```

---

## ✅ Completed: LwxSetting Mechanism (December 2025)

Replaced `[FromConfig]` constructor parameter injection with `[LwxSetting]` static partial properties:

- **`[LwxSetting("Key")]` attribute** - Marks static partial properties for configuration injection
- **Partial property generation** - Generator creates backing fields named `__N_Key`
- **Service.ConfigureSettings(builder)** - Reads configuration and sets backing fields via reflection

### Diagnostics
- `LWX030` - LwxSetting must be on static property
- `LWX031` - LwxSetting must be on partial property
- `LWX032` - LwxSetting property must be getter-only
- `LWX033` - LwxSetting unsupported type (only primitives allowed)
- `LWX034` - LwxSetting missing key
- `LWX035` - LwxSetting containing type must be partial

---

## ✅ Completed: Multi-Service Architecture (December 2025)

Support for multiple services in a single solution with namespace-based association:

- **Multiple `[LwxService]`** - Each namespace prefix can have its own service
- **Namespace-based routing**:
  - `Assembly.Abc.Endpoints...` → `Assembly.Abc.Service`
  - `Assembly.Abc.Workers...` → `Assembly.Abc.Service`

### Diagnostics
- `LWX017` - Duplicate Service for same namespace prefix
- `LWX020` - Service namespace must be under assembly namespace
- `LWX021` - Orphan Endpoint (no matching service)
- `LWX022` - Orphan Worker (no matching service)

---

## ✅ Removed: ServiceBus/EventHub Processors (December 2025)

Removed unused stub processors for Azure ServiceBus and EventHub:
- `LwxServiceBusConsumerAttribute` / Processor
- `LwxServiceBusProducerAttribute` / Processor
- `LwxEventHubConsumerAttribute` / Processor

These were placeholder stubs. The `LwxMessageEndpoint` mechanism provides a generic abstraction that can work with any queue provider including Azure ServiceBus via custom `ILwxQueueProvider` implementations.
