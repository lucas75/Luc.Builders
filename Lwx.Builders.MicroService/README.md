# Lwx.Builders.MicroService

A Roslyn incremental source generator for building ASP.NET Core microservices with minimal boilerplate. Define endpoints and background workers using attributes, and the generator handles routing, DI wiring, and Swagger documentation.

## Installation

Reference the generator in your project:

```xml
<ProjectReference Include="../Lwx.Builders.MicroService/Lwx.Builders.MicroService.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Quick Start

### 1. Create a Service

Every microservice needs a `Service.cs` file with the `[LwxService]` attribute:

```csharp
using Lwx.Builders.MicroService.Atributtes;

namespace MyCompany.MyService;

[LwxService(
    Title = "My Service",
    Description = "API for my service",
    Version = "1.0.0",
    PublishSwagger = LwxStage.DevelopmentOnly
)]
public partial class Service { }
```

### 2. Create Endpoints

Place endpoints in an `Endpoints/` folder. The class name determines the route, and the attribute goes on the `Execute` method:

```csharp
using Lwx.Builders.MicroService.Atributtes;

namespace MyCompany.MyService.Endpoints;

public static partial class EndpointHello
{
    [LwxEndpoint(
        Uri = "GET /hello",
        SecurityProfile = "public",
        Summary = "Hello World",
        Publish = LwxStage.All
    )]
    public static Task<string> Execute([FromQuery] string? name)
    {
        return Task.FromResult($"Hello, {name ?? "World"}!");
    }
}
```

### 3. Create Workers (Optional)

Background workers go in a `Workers/` folder:

```csharp
using Lwx.Builders.MicroService.Atributtes;

namespace MyCompany.MyService.Workers;

[LwxWorker(
    Stage = LwxStage.All,
    Threads = 2,
    Policy = LwxWorkerPolicy.UnhealthyIfExit
)]
public partial class MyWorker(ILogger<MyWorker> logger) : BackgroundService
{
    [LwxSetting("MyWorker:Interval")]
    public static partial int IntervalMs { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker running at {Time}", DateTimeOffset.Now);
            await Task.Delay(IntervalMs > 0 ? IntervalMs : 5000, stoppingToken);
        }
    }
}
```

## Project Structure

```
MyCompany.MyService/
├── Service.cs              # [LwxService] - required
├── Endpoints/
│   ├── EndpointHello.cs    # GET /hello
│   ├── EndpointUsers.cs    # GET /users
│   └── Admin/
│       └── EndpointAdminStats.cs  # GET /admin/stats
└── Workers/
    └── MyWorker.cs
```

## Endpoint Reference

### URI Patterns

```csharp
[LwxEndpoint(Uri = "GET /users")]           // Simple route
[LwxEndpoint(Uri = "POST /users")]          // POST method
[LwxEndpoint(Uri = "GET /users/{id}")]      // Path parameter
[LwxEndpoint(Uri = "GET /users/{id}/posts/{postId}")]  // Multiple parameters
```

All attributes go on the `Execute` method, not the class.

### Class Naming Convention

The endpoint class name must match the URI pattern:

| URI | Class Name |
|-----|------------|
| `GET /hello` | `EndpointHello` |
| `GET /users/{id}` | `EndpointUsersParamId` |
| `POST /abc/cde` | `EndpointAbcCde` or `EndpointAbcCdePost` |

### Execute Method Parameters

```csharp
public static Task<ResponseDto> Execute(
    [FromQuery] string? filter,           // Query string: ?filter=value
    [FromQuery] int page = 1,             // With default
    [FromBody] RequestDto body,           // Request body
    [FromRoute] int id,                   // Path parameter /{id}
    [FromHeader] string authorization,    // HTTP header
    [FromServices] IMyService service,    // DI injection
    CancellationToken ct                  // Cancellation token
) { }
```

### Stage Configuration

```csharp
// Endpoint available in all environments
Publish = LwxStage.All

// Endpoint only in Development (useful for debug/test endpoints)
Publish = LwxStage.DevelopmentOnly

// Endpoint disabled (not registered)
Publish = LwxStage.None
```

## Configuration Settings with [LwxSetting]

Use static partial properties with `[LwxSetting]` to read configuration values:

```csharp
public partial class MyWorker(ILogger<MyWorker> logger) : BackgroundService
{
    [LwxSetting("MyWorker:ConnectionString")]
    public static partial string ConnectionString { get; }
    
    [LwxSetting("MyWorker:BatchSize")]
    public static partial int BatchSize { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Using connection: {conn}", ConnectionString);
        // ...
    }
}
```

Configuration is read from `appsettings.json`:

```json
{
  "MyWorker": {
    "ConnectionString": "...",
    "BatchSize": 50
  }
}
```

**Supported types:** `string`, `int`, `bool`, `double`, `long`, `float`, `decimal`

**Key format:** Use `:` as separator for nested sections (e.g., `"Section:SubSection:Key"`).

The backing fields are populated during `Service.Configure(WebApplicationBuilder)` before any services are registered.

### Worker Policies

```csharp
// Mark service unhealthy if worker exits unexpectedly
Policy = LwxWorkerPolicy.UnhealthyIfExit

// Mark unhealthy on unhandled exception
Policy = LwxWorkerPolicy.UnhealthyOnException

// Worker health doesn't affect service health
Policy = LwxWorkerPolicy.AlwaysHealthy
```

## Message Endpoints

Process messages from queues with optional HTTP endpoints for testing:

```csharp
// Services/MyQueueProvider.cs - Queue provider implementation
public class MyQueueProvider : ILwxQueueProvider
{
    public string Name => nameof(MyQueueProvider);
    public void Configure(IConfiguration config, string section) { /* ... */ }
    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy) { /* ... */ }
    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct) { /* ... */ }
    public Task StopAsync(CancellationToken ct) { /* ... */ }
}

// Endpoints/EndpointMsgReceiveOrder.cs - Message endpoint
public partial class EndpointMsgReceiveOrder
{
    [LwxMessageEndpoint(
        Uri = "POST /receive-order",
        QueueStage = LwxStage.All,           // Queue consumer runs everywhere
        UriStage = LwxStage.DevelopmentOnly, // HTTP endpoint only in dev
        QueueProvider = typeof(MyQueueProvider),
        QueueConfigSection = "OrderQueue",
        QueueReaders = 2
    )]
    public static Task Execute(
        ILwxQueueMessage msg,
        ILogger<EndpointMsgReceiveOrder> logger)
    {
        logger.LogInformation("Processing message {Id}", msg.MessageId);
        return msg.CompleteAsync().AsTask();
    }
}
```

### Naming Convention

Message endpoint classes must:
- Start with `EndpointMsg` prefix (e.g., `EndpointMsgReceiveOrder`)
- Be in a `.Endpoints` namespace
- Have a static `Execute` method annotated with `[LwxMessageEndpoint]` and an `ILwxQueueMessage` parameter

### Stage Configuration

Use separate stages for queue and HTTP:
- `QueueStage = LwxStage.All` - Queue consumer runs in all environments
- `QueueStage = LwxStage.DevelopmentOnly` - Queue consumer only in dev
- `UriStage = LwxStage.DevelopmentOnly` - HTTP endpoint only in dev (for testing)
- `UriStage = LwxStage.None` - No HTTP endpoint

## Timer Endpoints

Schedule recurring tasks with cron expressions or simple intervals. The attribute goes on the `Execute` method:

```csharp
// Interval-based timer - runs every 30 seconds
public static partial class EndpointTimerCleanup
{
    [LwxTimer(IntervalSeconds = 30, Summary = "Cleanup timer")]
    public static void Execute()
    {
        Console.WriteLine("Timer executed!");
    }
}

// Cron-based timer - runs every 5 minutes
public static partial class EndpointTimerHealthCheck
{
    [LwxTimer(
        CronExpression = "0 */5 * * * *",  // NCrontab 6-field format
        Stage = LwxStage.All,
        RunOnStartup = true
    )]
    public static async Task Execute(
        ILogger<EndpointTimerHealthCheck> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Health check at {Time}", DateTimeOffset.Now);
        await Task.Delay(100, ct);
    }
}
```

### Timer Attribute Properties

| Property | Description | Default |
|----------|-------------|---------|
| `IntervalSeconds` | Simple interval in seconds (takes precedence over cron) | 0 (disabled) |
| `CronExpression` | NCrontab 6-field format (second minute hour day month weekday) | - |
| `Stage` | Environment stage (All, DevelopmentOnly, None) | All |
| `RunOnStartup` | Run immediately on application start | false |
| `Summary` | Short description for documentation | - |
| `Description` | Detailed description | - |

### Cron Expression Format

Uses NCrontab 6-field format: `second minute hour day month weekday`

| Example | Description |
|---------|-------------|
| `0 */5 * * * *` | Every 5 minutes |
| `0 0 * * * *` | Every hour |
| `0 0 0 * * *` | Every day at midnight |
| `0 30 9 * * 1-5` | Weekdays at 9:30 AM |

### Naming Convention

Timer endpoint classes must:
- Start with `EndpointTimer` prefix (e.g., `EndpointTimerCleanup`)
- Be in a `.Endpoints` namespace
- Have a static `Execute` method annotated with `[LwxTimer]` (can be void or Task)

### DI Support

The Execute method can request services via parameters:

```csharp
public static async Task Execute(
    ILogger<EndpointTimerCleanup> logger,    // DI injection
    IMyService service,                       // Custom service
    CancellationToken ct                      // Cancellation token
) { }
```

Services are resolved using `IServiceProvider.CreateScope()` for proper scoped lifetime.

## Swagger Configuration

Swagger is configured via `[LwxService]`:

```csharp
[LwxService(
    Title = "My API",
    Description = "API documentation",
    Version = "1.0.0",
    PublishSwagger = LwxStage.DevelopmentOnly  // Only in Development
)]
```

Access Swagger UI at `/swagger` when running in Development.

## Diagnostics

The generator reports compile-time errors for common issues:

| Code | Issue |
|------|-------|
| LWX011 | Missing `Service.cs` file |
| LWX012 | `[LwxService]` not in `Service.cs` |
| LWX018 | Endpoint not in `.Endpoints` namespace |
| LWX019 | Worker not in `.Workers` namespace |
| LWX021 | Endpoint has no matching service |
| LWX040 | Invalid message endpoint class name |
| LWX042 | Message endpoint not in `.Endpoints` namespace |
| LWX043 | Missing QueueProvider |
| LWX060 | Invalid timer endpoint class name |
| LWX061 | Timer endpoint not in `.Endpoints` namespace |
| LWX070 | `[LwxEndpoint]` must be on Execute method |
| LWX071 | Endpoint attribute not on method named Execute |
| LWX072 | `[LwxMessageEndpoint]` must be on Execute method |
| LWX073 | Message endpoint attribute not on method named Execute |
| LWX074 | `[LwxTimer]` must be on Execute method |
| LWX075 | Timer attribute not on method named Execute |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for architecture details and development guidelines.