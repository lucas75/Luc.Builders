# TODO

## Pending Features

(None at this time)

---

# CHANGELOG

## ✅ Completed: ReqBodyType Property for HTTP Deserialization (January 2025)

Added `ReqBodyType` property to `[LwxEndpoint]` attribute for specifying concrete `ILwxQueueMessage` implementation used in HTTP deserialization.

### Purpose
When using `[LwxEndpoint]` with `[LwxMessageSource]`, ASP.NET Core cannot deserialize JSON into the `ILwxQueueMessage` interface directly. The `ReqBodyType` property specifies the concrete type that ASP.NET should use for model binding.

### Requirements
- The `ReqBodyType` must implement `ILwxQueueMessage`
- The `ReqBodyType` must have a parameterless constructor for model binding
- HTTP-specific methods (`CompleteAsync`, `AbandonAsync`, `DeadLetterAsync`) can be no-op stubs

### Example Usage
```csharp
// Create a concrete ILwxQueueMessage implementation for HTTP binding
public class OrderMessage : ILwxQueueMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("payload")]
    public string PayloadString { get; set; } = string.Empty;

    [JsonIgnore]
    public ReadOnlyMemory<byte> Payload => Encoding.UTF8.GetBytes(PayloadString);

    [JsonPropertyName("headers")]
    public Dictionary<string, string> HeadersDict { get; set; } = new();

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> Headers => HeadersDict;

    [JsonPropertyName("enqueuedAt")]
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;

    // No-op stubs for HTTP use
    public ValueTask CompleteAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask AbandonAsync(string? reason = null, CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask DeadLetterAsync(string? reason = null, CancellationToken ct = default) => ValueTask.CompletedTask;
}

// Use in endpoint
public partial class EndpointMsgReceiveOrder
{
    [LwxEndpoint("POST /receive-order", Publish = LwxStage.DevelopmentOnly, ReqBodyType = typeof(OrderMessage))]
    [LwxMessageSource(
        Stage = LwxStage.All,
        QueueProvider = typeof(ExampleQueueProvider),
        QueueConfigSection = "OrderQueue"
    )]
    public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
}
```

### New Diagnostic
- `LWX050` - When `[LwxMessageSource]` is combined with `[LwxEndpoint]`, the `ReqBodyType` property must be specified

### Processor Changes
-- `LwxEndpointTypeProcessor` now skips processing when `ReqBodyType` is set (defers to `LwxMessageSourceTypeProcessor`)
-- `LwxMessageSourceTypeProcessor` validates `ReqBodyType` is present when `[LwxEndpoint]` is combined with `[LwxMessageSource]`
-- HTTP endpoint generation uses `ReqBodyType` instead of generating a wrapper class

---

## ✅ Completed: Split LwxMessageSource from LwxEndpoint (January 2025)

Refactored from single `[LwxMessageEndpoint]` to dual-attribute pattern for cleaner separation of concerns.

### Breaking Change
- `[LwxMessageEndpoint]` is now replaced by `[LwxEndpoint]` + `[LwxMessageSource]`
    - HTTP configuration goes in `[LwxEndpoint]` (Uri, Publish, Summary, Description, ReqBodyType)
- Queue configuration goes in `[LwxMessageSource]` (Stage, QueueProvider, QueueConfigSection, QueueReaders, HandlerErrorPolicy, ProviderErrorPolicy)

### Migration Example

Before:
```csharp
[LwxMessageEndpoint(
    Uri = "POST /receive-order",
    QueueStage = LwxStage.All,
    UriStage = LwxStage.DevelopmentOnly,
    QueueProvider = typeof(MyQueueProvider),
    QueueConfigSection = "OrderQueue"
)]
public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
```

After:
```csharp
[LwxEndpoint("POST /receive-order", Publish = LwxStage.DevelopmentOnly, ReqBodyType = typeof(OrderMessage))]
[LwxMessageSource(
    Stage = LwxStage.All,
    QueueProvider = typeof(MyQueueProvider),
    QueueConfigSection = "OrderQueue"
)]
public static Task Execute(ILwxQueueMessage msg) => Task.CompletedTask;
```

---

## ✅ Completed: Method-Level Attributes (January 2025)

Moved all endpoint attributes from class level to method level for cleaner declaration.

### Breaking Change
- `[LwxEndpoint]` now goes on the `Execute` method, not the class
- `[LwxMessageSource]` now goes on the `Execute` method, not the class
- `[LwxTimer]` now goes on the `Execute` method, not the class

### New Diagnostics
- `LWX070` - `[LwxEndpoint]` attribute must be placed on the Execute method
- `LWX071` - Endpoint attribute not on method named Execute
- `LWX072` - `[LwxMessageSource]` attribute must be placed on the Execute method
- `LWX073` - Message source attribute not on method named Execute
- `LWX074` - `[LwxTimer]` attribute must be placed on the Execute method
- `LWX075` - Timer attribute not on method named Execute

### Migration Example

Before:
```csharp
[LwxEndpoint(Uri = "GET /hello")]
public static partial class EndpointHello
{
    public static Task<string> Execute() => Task.FromResult("Hello");
}
```

After:
```csharp
public static partial class EndpointHello
{
    [LwxEndpoint(Uri = "GET /hello")]
    public static Task<string> Execute() => Task.FromResult("Hello");
}
```

---

## ✅ Completed: LwxTimer Mechanism (January 2025)

Implemented timer-triggered endpoints with both interval-based and cron-based scheduling.

### `[LwxTimer]` Attribute Properties
- `CronExpression` - NCrontab 6-field format (second minute hour day month weekday)
- `IntervalSeconds` - Simple interval in seconds (takes precedence over CronExpression)
- `Stage` - Stage controls which environments the timer runs in
- `RunOnStartup` - Whether to run immediately on application start
- `Summary` - Short description for documentation
- `Description` - Detailed description
- `NamingExceptionJustification` - Justification for non-standard naming

### Naming Convention
- Classes: `EndpointTimer{Name}` (e.g., `EndpointTimerCleanup`)
- Namespace: Must be under `.Endpoints` (same as regular endpoints)

### Generated Code
- Hosted `BackgroundService` with either interval-based or cron-based scheduling
- Service wiring via `ConfigureTimers(builder)`
- Timer listing in startup output
- DI parameter resolution for Execute method

### New Diagnostics
- `LWX060` - Invalid timer endpoint class name
- `LWX061` - Invalid timer endpoint namespace

### Example Usage

```csharp
// Interval-based timer - every 30 seconds
public static partial class EndpointTimerCleanup
{
    [LwxTimer(IntervalSeconds = 30, Summary = "Cleanup timer")]
    public static void Execute()
    {
        Console.WriteLine("Timer executed!");
    }
}

// Cron-based timer - every 5 minutes
public static partial class EndpointTimerHealthCheck
{
    [LwxTimer(CronExpression = "0 */5 * * * *", RunOnStartup = true)]
    public static async Task Execute(ILogger<EndpointTimerHealthCheck> logger, CancellationToken ct)
    {
        logger.LogInformation("Health check at {Time}", DateTimeOffset.Now);
        await Task.Delay(100, ct);
    }
}
```

---

## ✅ Completed: LwxMessageSource Mechanism (December 2025)

Implemented message queue processing with configurable providers and error policies.
Refactored to use dual-attribute pattern: `[LwxEndpoint]` + `[LwxMessageSource]`.

### Core Interfaces
- `ILwxQueueProvider` - Queue provider abstraction with Configure/Start/Stop lifecycle
- `ILwxQueueMessage` - Message abstraction with Complete/Abandon/DeadLetter lifecycle
- `ILwxErrorPolicy` - Handler-level error policy for message processing errors
- `ILwxProviderErrorPolicy` - Provider-level error policy for connection/worker errors

### Default Policies
- `LwxDefaultErrorPolicy` - Dead-letters messages on handler errors
- `LwxDefaultProviderErrorPolicy` - Logs provider errors

### Attribute Properties

`[LwxEndpoint]` (for HTTP):
- `Uri` - HTTP endpoint path for receiving messages (e.g., "POST /receive-order")
- `Publish` - Stage for HTTP endpoint (None, DevelopmentOnly, All)
- `Summary` - Short description
- `Description` - Detailed description

`[LwxMessageSource]` (for queue):
- `Stage` - Stage for queue consumer (None, DevelopmentOnly, All)
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
public partial class EndpointMsgReceiveOrder
{
    [LwxEndpoint("POST /receive-order", Publish = LwxStage.DevelopmentOnly)]
    [LwxMessageSource(
        Stage = LwxStage.All,           // Queue runs everywhere
        QueueProvider = typeof(MyQueueProvider),
        QueueConfigSection = "OrderQueue",
        QueueReaders = 2
    )]
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

These were placeholder stubs. The `LwxMessageSource` mechanism provides a generic abstraction that can work with any queue provider including Azure ServiceBus via custom `ILwxQueueProvider` implementations.
