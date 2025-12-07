# TODO

## Feature Request: MessageProcessor Mechanism

A mechanism to parse messages received from message queues or http endpoints.

```csharp

public interface ILwxQueueProvider {
   // Human friendly name for diagnostics
   string Name { get; }

   // Called by runtime/generator wiring to provide configuration for the named section.
   // Example: sectionName="MyQueueA" corresponds to appsettings: "Queues:MyQueueA:{...}"
   void Configure(Microsoft.Extensions.Configuration.IConfiguration configuration, string sectionName);

   // Set provider-level error policy (connection / worker errors)
   void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy);

   // Start/stop the worker that reads/messages and invokes handler
   Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct);
   Task StopAsync(CancellationToken ct);
}

public interface ILwxQueueMessage {
   // Message properties and lifecycle methods
   string MessageId { get; }
   ReadOnlyMemory<byte> Payload { get; }
   IReadOnlyDictionary<string,string> Headers { get; }
   DateTimeOffset EnqueuedAt { get; }

   ValueTask CompleteAsync(CancellationToken ct = default);
   ValueTask AbandonAsync(string? reason = null, CancellationToken ct = default);
   ValueTask DeadLetterAsync(string? reason = null, CancellationToken ct = default);
}

// Errors thrown by the handler (message-processing) are handled by this policy.
public interface ILwxErrorPolicy {
   Task HandleErrorAsync(ILwxQueueMessage msg, Exception ex, CancellationToken ct = default);
}

// Errors at the provider/worker level (connections, topology, background failures).
public interface ILwxProviderErrorPolicy {
   Task HandleProviderErrorAsync(Exception ex, CancellationToken ct = default);
}

// Example in-memory provider as a driver for tests/samples.
// Reads settings from the named section "Queues:{sectionName}".
public class ExampleQueue : ILwxQueueProvider {
   private readonly System.Threading.Channels.Channel<ILwxQueueMessage> _channel = System.Threading.Channels.Channel.CreateUnbounded<ILwxQueueMessage>();

   public string Name => nameof(ExampleQueue);

   public void Configure(Microsoft.Extensions.Configuration.IConfiguration configuration, string sectionName) {
      // Read settings from configuration.GetSection($"Queues:{sectionName}") ...
      // e.g. Readers, MaxBatchSize, VisibilityTimeout, etc.
   }

   public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { /* omitted */ }

   public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) { /* omitted */ }
   public Task StopAsync(CancellationToken ct) { /* omitted */ }
}

// Attribute usage: generator/runtime should validate types and config section existence.
[LwxMessageHandler(
   Uri = "POST /receive-message",
   Stage = LwxStage.All,
   QueueProvider = typeof(ExampleQueue),        // driver type that implements ILwxQueueProvider
   QueueConfigSection = "MyExampleQueue",       // named section: "Queues:MyExampleQueue"
   QueueReaders = 2,                            // concurrency (can be overridden in provider config)
   HandlerErrorPolicy = typeof(DefaultHandlerErrorPolicy),
   ProviderErrorPolicy = typeof(DefaultProviderErrorPolicy)
)]
public partial class EndpointReceiveMessage {
   public static Task Execute(ILwxQueueMessage msg) { /* omitted */ }
}

// Default policies (bodies omitted)
public class DefaultHandlerErrorPolicy : ILwxErrorPolicy { /* omitted */ }
public class DefaultProviderErrorPolicy : ILwxProviderErrorPolicy { /* omitted */ }
```

Configuration sample (appsettings.json)
```json
{
  "Queues": {
    "MyExampleQueue": {
      "Readers": 2,
      "SomeProviderSpecificSetting": "value"
    }
  }
}
```

Runtime/Generator checklist (summary):
- Validate QueueProvider implements ILwxQueueProvider (emit diagnostic if not).
- Validate QueueConfigSection exists in configuration (emit diagnostic if missing).
- Resolve provider from DI or instantiate and call Configure(configuration, QueueConfigSection).
- Set ProviderErrorPolicy and HandlerErrorPolicy; pass handler wrapper that captures policy handling.
- Start provider with configured concurrency (QueueReaders or value from config).
- On provider errors, the provider should call the ProviderErrorPolicy; generator/runtime may use the policy to decide to restart, backoff, or shut down service.

# CHANGELOG

## ✅ Completed: LwxSetting Mechanism (December 2025)

Replaced `[FromConfig]` constructor parameter injection with `[LwxSetting]` static partial properties:

1. ✅ **New `[LwxSetting("Key")]` attribute** - Marks static partial properties for configuration injection
2. ✅ **Partial property generation** - Generator creates backing fields named `__N_Key`
3. ✅ **Service.ConfigureSettings(builder)** - Reads configuration and sets backing fields via reflection
4. ✅ **Removed `[FromConfig]` mechanism** - Simplified worker registration (no more factory-based DI)
5. ✅ **Updated sample projects and tests** - Migrated to new mechanism

### New Diagnostics
- `LWX030` - LwxSetting must be on static property
- `LWX031` - LwxSetting must be on partial property
- `LWX032` - LwxSetting property must be getter-only
- `LWX033` - LwxSetting unsupported type (only primitives allowed)
- `LWX034` - LwxSetting missing key
- `LWX035` - LwxSetting containing type must be partial

### Key Implementation Files
- `Attributes/LwxSettingAttribute.cs` - New attribute definition
- `Processors/LwxSettingTypeProcessor.cs` - Property processor
- `Processors/LwxSettingPostInitializationProcessor.cs` - Embeds attribute
- `Processors/LwxServiceTypeProcessor.cs` - Generates ConfigureSettings method
- `Processors/LwxWorkerTypeProcessor.cs` - Simplified (removed FromConfig handling)

---

## ✅ Completed: Multi-Service Architecture (December 2025)

The following changes have been implemented:

1. ✅ **Allow multiple LwxService** - Each namespace prefix can have its own service
2. ✅ **Associate Endpoints and Workers to LwxService by namespace**:
   - `AssemblyNamespace.Abc.Endpoints...` → `AssemblyNamespace.Abc.Service`
   - `AssemblyNamespace.Abc.Workers...` → `AssemblyNamespace.Abc.Service`
   - `AssemblyNamespace.Cde.Endpoints...` → `AssemblyNamespace.Cde.Service`
   - `AssemblyNamespace.Cde.Workers...` → `AssemblyNamespace.Cde.Service`
3. ✅ **Generated output**:
   - `AssemblyNamespace.Abc.Service.Configure(...)` - wires only Abc's endpoints/workers
   - `AssemblyNamespace.Cde.Service.Configure(...)` - wires only Cde's endpoints/workers
4. ✅ **Namespace validation**: Service outside assembly root only allowed in test/lib projects
   - Detection via: assembly name patterns (`.Tests`, `.Lib`), absence of `Program.cs`, `OutputKind`

### New Diagnostics
- `LWX017` - Duplicate Service for same namespace prefix
- `LWX020` - Service namespace must be under assembly namespace
- `LWX021` - Orphan Endpoint (no matching service)
- `LWX022` - Orphan Worker (no matching service)

### Key Implementation Files
- `Generator.cs` - New `ServiceRegistration` class and `ServiceRegistrations` dictionary
- `LwxServiceTypeProcessor.cs` - Rewritten to use service registrations
- `LwxEndpointTypeProcessor.cs` - Registers endpoints to appropriate service by namespace
- `LwxWorkerTypeProcessor.cs` - Registers workers to appropriate service by namespace

---

## ✅ Completed: MessageHandler Mechanism (January 2025)

Implemented message queue processing with configurable providers and error policies:

1. ✅ **Core Interfaces**:
   - `ILwxQueueProvider` - Queue provider abstraction with Configure/Start/Stop lifecycle
   - `ILwxQueueMessage` - Message abstraction with Complete/Abandon/DeadLetter lifecycle
   - `ILwxErrorPolicy` - Handler-level error policy for message processing errors
   - `ILwxProviderErrorPolicy` - Provider-level error policy for connection/worker errors

2. ✅ **Default Policies**:
   - `LwxDefaultErrorPolicy` - Logs and abandons messages on handler errors
   - `LwxDefaultProviderErrorPolicy` - Logs provider errors

3. ✅ **`[LwxMessageHandler]` Attribute** with properties:
   - `Uri` - HTTP endpoint path for receiving messages (e.g., "POST /receive-order")
   - `Stage` - Execution stage (Dev, Test, Prod, All, NonProd)
   - `QueueProvider` - Type implementing `ILwxQueueProvider`
   - `QueueConfigSection` - Configuration section name (reads from `Queues:{section}`)
   - `QueueReaders` - Concurrency level (default: 1)
   - `HandlerErrorPolicy` - Type implementing `ILwxErrorPolicy` (optional)
   - `ProviderErrorPolicy` - Type implementing `ILwxProviderErrorPolicy` (optional)

4. ✅ **Generated Code**:
   - Hosted background service for queue consumption
   - HTTP endpoint for manual message submission
   - Service wiring via `ConfigureMessageHandlers(builder)` and `ConfigureMessageHandlers(app)`

5. ✅ **Namespace Convention**: MessageHandlers must be in `.MessageHandlers` namespace
   - Similar to `.Endpoints` and `.Workers` patterns

### New Diagnostics
- `LWX040` - MessageHandler must be partial class
- `LWX041` - MessageHandler must have Execute method
- `LWX042` - MessageHandler.Execute must be static
- `LWX043` - MessageHandler.Execute must return Task
- `LWX044` - MessageHandler.Execute must have ILwxQueueMessage parameter
- `LWX045` - QueueProvider must implement ILwxQueueProvider
- `LWX046` - HandlerErrorPolicy must implement ILwxErrorPolicy
- `LWX047` - ProviderErrorPolicy must implement ILwxProviderErrorPolicy
- `LWX048` - MessageHandler must be in MessageHandlers namespace
- `LWX049` - Orphan MessageHandler (no matching service)
- `LWX050` - Class in MessageHandlers namespace requires attribute or interface
- `LWX051` - MessageHandler Execute must have CancellationToken parameter

### Key Implementation Files
- `Attributes/ILwxQueueMessage.cs` - Message interface
- `Attributes/ILwxQueueProvider.cs` - Provider interface
- `Attributes/ILwxErrorPolicy.cs` - Handler error policy interface
- `Attributes/ILwxProviderErrorPolicy.cs` - Provider error policy interface
- `Attributes/LwxDefaultErrorPolicy.cs` - Default handler error policy
- `Attributes/LwxDefaultProviderErrorPolicy.cs` - Default provider error policy
- `Attributes/LwxMessageHandlerAttribute.cs` - MessageHandler attribute
- `Attributes/LwxMessageHandlerDescriptor.cs` - Runtime descriptor
- `Processors/LwxMessageHandlerTypeProcessor.cs` - Type processor
- `Processors/LwxMessageHandlerPostInitializationProcessor.cs` - Embeds interfaces/attribute
- `Generator.cs` - ServiceRegistration updated with MessageHandlerNames/Infos
- `LwxServiceTypeProcessor.cs` - Generates ConfigureMessageHandlers methods

### Example Usage

```csharp
// In MessageHandlers namespace
[LwxMessageHandler(
    Uri = "POST /receive-order",
    Stage = LwxStage.All,
    QueueProvider = typeof(ExampleQueueProvider),
    QueueConfigSection = "OrderQueue",
    QueueReaders = 2
)]
public partial class MessageHandlerReceiveOrder
{
    public static Task Execute(ILwxQueueMessage msg, CancellationToken ct)
    {
        // Process the message
        return msg.CompleteAsync(ct).AsTask();
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
