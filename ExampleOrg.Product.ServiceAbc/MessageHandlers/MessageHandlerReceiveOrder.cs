using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.MessageHandlers;

/// <summary>
/// Example message handler that processes order messages from a queue.
/// Demonstrates the LwxMessageHandler pattern with optional HTTP endpoint.
/// </summary>
[LwxMessageHandler(
    Uri = "POST /receive-order",
    Stage = LwxStage.DevelopmentOnly,
    QueueProvider = typeof(ExampleQueueProvider),
    QueueConfigSection = "OrderQueue",
    QueueReaders = 2,
    Summary = "Receive Order Handler",
    Description = "Processes incoming order messages from the queue or HTTP POST"
)]
public partial class MessageHandlerReceiveOrder
{
    /// <summary>
    /// Processes an incoming order message.
    /// </summary>
    /// <param name="msg">The queue message containing order data.</param>
    public static Task Execute(ILwxQueueMessage msg)
    {
        var payloadText = Encoding.UTF8.GetString(msg.Payload.Span);
        
        Console.WriteLine($"[MessageHandlerReceiveOrder] Processing order message:");
        Console.WriteLine($"  MessageId: {msg.MessageId}");
        Console.WriteLine($"  EnqueuedAt: {msg.EnqueuedAt}");
        Console.WriteLine($"  Payload: {payloadText}");
        Console.WriteLine($"  Headers: {msg.Headers.Count} entries");
        
        // Simulate order processing
        // In a real application, you would:
        // 1. Parse the payload (JSON, XML, etc.)
        // 2. Validate the order data
        // 3. Save to database
        // 4. Trigger downstream processes
        
        return Task.CompletedTask;
    }
}
