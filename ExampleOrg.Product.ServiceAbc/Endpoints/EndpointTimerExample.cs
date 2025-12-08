using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.Endpoints;

/// <summary>
/// Example timer that runs every 30 seconds.
/// </summary>
[LwxTimer(IntervalSeconds = 30, Summary = "Example interval timer")]
public static partial class EndpointTimerExample
{
    public static void Execute()
    {
        System.Console.WriteLine($"[{DateTime.UtcNow:O}] Timer executed!");
    }
}
