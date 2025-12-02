# Lwx.Builders.MicroService

The objective of the `Lwx.Builders.MicroService` is to provide a framework for building microservices with endpoints and background workers. It leverages attributes to define endpoints and workers, making it easy to create and manage microservices.

## Source Code Organization

```text
  ExampleOrg.Product.ServiceAbc/          
    Endpoints/                         
      EndpointHello.cs     -> /hello              
      EndpointAbcCde.cs    -> /abc/cde
      Efg/
        EndpointEfgHij.cs  -> /efg/hij
    Workers/                           
      TheWorker.cs               
```

## Endpoints

The example below demonstrates how to create an endpoint using the `LwxEndpoint` attribute.

```csharp
namespace ExampleOrg.Product.Service.Workers;

[LwxEndpoint(
  // The HTTP method and URI for the endpoint (required)
  Uri = "GET /hello",
  // The security profile for the endpoint (required)
  SecurityProfile = "public",
  // A short summary of the endpoint (optional)
  Summary = "Hello World Endpoint",
  // A detailed description of the endpoint (optional)
  Description = "It will say hello to the world",
  // The stage in which the endpoint will be published (required)  
  // - DevelopmentOnly
  // - All
  // - None
  Publish = LwxStage.DevelopmentOnly
)]
public static partial class EndpointHello
{
  public async static Task<string> Execute(
      [FromQuery] string? param1
  )
  {
      // pretend is doing work
      await Task.CompletedTask;
      return "Hello, World!";
  }
}
```

The Lwx.Builder.MicroService will automatically register the worker and run it according to the specified stage.


## Workers 

The example bellow demonstrates how to create a worker using the `LwxWorker` attribute.

```csharp
namespace ExampleOrg.Product.Service.Workers;

[LwxWorker(
  // The worker name (optional: defaults to the class name)
  Name="TheWorker", 
  // The worker description (optional: defaults to empty string)
  Description="This is a sample worker", 
  // The number of threads (optional: defaults to 2)
  Threads=4,
  // Policy - It defines the health check policy for the worker (required)
  // - UnhealthyIfExit
  // - UnhealthyOnException
  // - AlwaysHealthy  
  Policy=LwxWorkerPolicy.UnhealthyIfExit,
  // Stage - It defines the stage in which the worker will run (required)
  // - DevelopmentOnly
  // - All
  // - None
  Stage=LwxStage.All
  )
]
public partial class TheWorker (
  ILogger<TheWorker> logger,
  [FromConfig("Abc")] string abc,
  // Other dependencies
) : BackgroundService
{
  public override Task ExecuteAsync(
    CancellationToken stoppingToken
  )
  {
    logger.LogInformation("SampleWorker started.");

    while (!stoppingToken.IsCancellationRequested)
    {
        // Your worker logic here
        logger.LogInformation("SampleWorker running at: {time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }

    logger.LogInformation("SampleWorker stopping.");
  }
}
```

Example in the configuration file:
```json
{  
  // the configuration section is the name of the worker
  "TheWorker": { 
    // this will override the attribute value
    "Description": "This is a sample worker",
    // this will override the attribute value
    "Threads": 4,
    // this is the example of a custom setting
    "Abc": "Xyz" 
  }
}
```

The Lwx.Builder.MicroService will automatically register the worker and run it according to the specified stage.