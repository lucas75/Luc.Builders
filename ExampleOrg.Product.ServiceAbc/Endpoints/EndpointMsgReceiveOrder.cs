using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;
using ExampleOrg.Product.ServiceAbc.Workers;
using ExampleOrg.Product.ServiceAbc.Services;

namespace ExampleOrg.Product.ServiceAbc.Endpoints;

/// <summary>
/// Example message endpoint that processes order messages from a queue.
/// Demonstrates DI injection of workers and configuration services.
/// </summary>
/// <remarks>
/// The HTTP endpoint is exposed only in development for testing purposes.
/// The queue consumer runs in all environments (dev and prod).
/// </remarks>
public partial class EndpointMsgReceiveOrder
{
    /// <summary>
    /// Process an incoming order message.
    /// The ILwxQueueMessage is provided by the queue infrastructure.
    /// TheWorker and IConfiguration are injected via DI.
    /// </summary>
    [LwxMessageEndpoint(
        Uri = "POST /receive-order",
        QueueStage = LwxStage.All,
        UriStage = LwxStage.DevelopmentOnly,
        QueueProvider = typeof(ExampleQueueProvider),
        QueueConfigSection = "OrderQueue",
        QueueReaders = 2,
        Summary = "Receives order messages from queue",
        Description = "Processes incoming order messages. Can be triggered via HTTP in development for testing."
    )]
    public static Task Execute(
        ILwxQueueMessage msg, 
        ILogger<EndpointMsgReceiveOrder> logger,
        IConfiguration config)
    {
        var payload = Encoding.UTF8.GetString(msg.Payload.Span);
        var queueConfig = config.GetSection("Queues:OrderQueue");
        var maxRetries = queueConfig.GetValue<int>("MaxRetries", 3);

        logger.LogInformation(
            "Processing order message {MessageId} with payload: {Payload}, MaxRetries configured: {MaxRetries}",
            msg.MessageId,
            payload,
            maxRetries);

        // Example: Delegate some work to the worker or process directly
        // In a real scenario, you might parse the payload and dispatch to appropriate handlers

        return Task.CompletedTask;
    }
}
